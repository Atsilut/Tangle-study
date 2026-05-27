using Api.Domain.Chat.Domain;
using Api.Domain.Chat.Dto;
using Api.Domain.Chat.Repository;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Chat.Service;

[Service]
public class ChatRoomService
{
    private readonly IChatRoomRepository _repo;
    private readonly ChatRoomAccessService _access;
    private readonly UserService _userService;
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChatRoomService(
        IChatRoomRepository repo,
        ChatRoomAccessService access,
        UserService userService,
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor)
    {
        _repo = repo;
        _access = access;
        _userService = userService;
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));

    public async Task<ChatRoomGetResponseDto> GetOrCreateDirectRoomAsync(ChatRoomDirectCreateRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        await _access.EnsureCanCreateDirectRoomAsync(userId, request.OtherUserId);

        var existing = await _repo.GetDirectChatRoomForUserPairAsync(userId, request.OtherUserId);
        if (existing is not null)
            return await MapToGetDtoAsync(existing, includeParticipants: true);

        var room = ChatRoom.CreateDirect(userId, request.OtherUserId, userId);
        await CreateRoomWithParticipantsAsync(
            room,
            [
                (userId, ChatRoomParticipantRole.Member),
                (request.OtherUserId, ChatRoomParticipantRole.Member),
            ]);

        return await MapToGetDtoAsync(
            (await _repo.GetChatRoomByIdAsync(room.Id, includeParticipants: true))!,
            includeParticipants: true);
    }

    public async Task<ChatRoomGetResponseDto> CreateMultiRoomAsync(ChatRoomMultiCreateRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        var participantIds = NormalizeParticipantIds(userId, request.ParticipantUserIds);
        EnsureAtLeastOneOtherParticipant(userId, participantIds);

        await _access.EnsureCanCreateMultiRoomAsync(userId, participantIds);

        var room = ChatRoom.CreateMulti(request.Title, userId);
        var participants = BuildMemberOnlyParticipantSpecs(participantIds);
        await CreateRoomWithParticipantsAsync(room, participants);

        return await MapToGetDtoAsync(
            (await _repo.GetChatRoomByIdAsync(room.Id, includeParticipants: true))!,
            includeParticipants: true);
    }

    public async Task<ChatRoomGetResponseDto> CreatePlatformGroupRoomAsync(
        long platformGroupId,
        ChatRoomPlatformGroupCreateRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        var participantIds = NormalizeParticipantIds(userId, request.ParticipantUserIds);
        EnsureAtLeastOneOtherParticipant(userId, participantIds);

        await _access.EnsureCanCreatePlatformGroupRoomAsync(platformGroupId, userId, participantIds);

        var room = ChatRoom.CreatePlatformGroup(request.Title, platformGroupId, userId);
        var participants = BuildPlatformGroupParticipantSpecs(userId, participantIds);
        await CreateRoomWithParticipantsAsync(room, participants);

        return await MapToGetDtoAsync(
            (await _repo.GetChatRoomByIdAsync(room.Id, includeParticipants: true))!,
            includeParticipants: true);
    }

    public async Task<List<ChatRoomSummaryGetResponseDto>?> GetMyRoomsAsync()
    {
        var userId = GetUserIdFromLogin();
        var rooms = await _repo.GetChatRoomsForUserAsync(userId);
        if (rooms.Count == 0) return null;
        return rooms.Select(MapToSummaryDto).ToList();
    }

    public async Task<List<ChatRoomSummaryGetResponseDto>?> GetPlatformGroupRoomsAsync(long platformGroupId)
    {
        var userId = GetUserIdFromLogin();
        await _access.EnsureGroupMemberCanListRoomsAsync(platformGroupId, userId);

        var rooms = await _repo.GetChatRoomsForPlatformGroupAsync(platformGroupId);
        if (rooms.Count == 0) return null;
        return rooms.Select(MapToSummaryDto).ToList();
    }

    public async Task<ChatRoomGetResponseDto> GetRoomByIdAsync(long roomId)
    {
        await EnsureCurrentUserIsParticipantAsync(roomId);
        var room = await GetRoomWithParticipantsOrThrowAsync(roomId);
        return await MapToGetDtoAsync(room, includeParticipants: true);
    }

    public Task EnsureCurrentUserIsParticipantAsync(long roomId) =>
        EnsureUserIsParticipantAsync(roomId, GetUserIdFromLogin());

    public async Task EnsureUserIsParticipantAsync(long roomId, long userId)
    {
        var room = await GetRoomWithParticipantsOrThrowAsync(roomId);
        _access.EnsureParticipantInRoom(room.Participants.ToList(), userId);
    }

    public Task TouchRoomUpdatedAtAsync(long roomId) => _repo.TouchChatRoomUpdatedAtAsync(roomId);

    public async Task<ChatRoomParticipantGetResponseDto> AddParticipantAsync(
        long roomId,
        ChatRoomParticipantAddRequestDto request)
    {
        var userId = GetUserIdFromLogin();

        ChatRoomParticipant newParticipant = null!;
        await _db.ExecuteInTransactionAsync(async () =>
        {
            var room = await _db.ChatRooms
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.Id == roomId)
                ?? throw new EntityNotFoundException("Chat room not found");

            var participants = room.Participants.ToList();
            _access.EnsureCanAddParticipant(room, participants, userId);

            if (participants.Any(p => p.UserId == request.UserId))
                throw new EntityAlreadyExistsException("User is already a participant in this chat room.");

            await _access.EnsureInviteeCanBeAddedAsync(room, request.UserId, participants);

            if (room.Kind == ChatRoomKind.Direct)
                room.PromoteDirectToMulti();

            newParticipant = new ChatRoomParticipant(roomId, request.UserId, ChatRoomParticipantRole.Member);
            _db.ChatRoomParticipants.Add(newParticipant);
            room.TouchUpdatedAt();
            await _db.SaveChangesAsync();
        });

        var nicknames = await _userService.GetNicknamesByUserIdsAsync([request.UserId]);
        return MapParticipantToDto(
            newParticipant,
            nicknames.GetValueOrDefault(request.UserId, "Deleted User"));
    }

    public async Task LeaveRoomAsync(long roomId)
    {
        var userId = GetUserIdFromLogin();
        var participant = await _repo.GetChatRoomParticipantAsync(roomId, userId)
            ?? throw new EntityNotFoundException("Chat room not found");

        await _repo.RemoveChatRoomParticipantAsync(participant);
        await _repo.TouchChatRoomUpdatedAtAsync(roomId);
    }

    private async Task<ChatRoom> GetRoomWithParticipantsOrThrowAsync(long roomId) =>
        await _repo.GetChatRoomByIdAsync(roomId, includeParticipants: true)
        ?? throw new EntityNotFoundException("Chat room not found");

    private async Task CreateRoomWithParticipantsAsync(
        ChatRoom room,
        IReadOnlyList<(long UserId, ChatRoomParticipantRole Role)> participants)
    {
        await _db.ExecuteInTransactionAsync(async () =>
        {
            _db.ChatRooms.Add(room);
            await _db.SaveChangesAsync();

            foreach (var (participantUserId, role) in participants)
            {
                _db.ChatRoomParticipants.Add(new ChatRoomParticipant(room.Id, participantUserId, role));
            }

            await _db.SaveChangesAsync();
        });
    }

    private static List<long> NormalizeParticipantIds(long creatorId, IReadOnlyList<long> requestedIds)
    {
        var ids = new HashSet<long>(requestedIds) { creatorId };
        return ids.ToList();
    }

    private static void EnsureAtLeastOneOtherParticipant(long creatorId, IReadOnlyList<long> participantIds)
    {
        if (participantIds.Count < 2 || participantIds.All(id => id == creatorId))
            throw new ArgumentException("A group chat room must include at least one other participant.");
    }

    private static List<(long UserId, ChatRoomParticipantRole Role)> BuildMemberOnlyParticipantSpecs(
        IReadOnlyList<long> participantIds) =>
        participantIds.Select(id => (id, ChatRoomParticipantRole.Member)).ToList();

    private static List<(long UserId, ChatRoomParticipantRole Role)> BuildPlatformGroupParticipantSpecs(
        long creatorId,
        IReadOnlyList<long> participantIds) =>
        participantIds
            .Select(id => (id, id == creatorId ? ChatRoomParticipantRole.Owner : ChatRoomParticipantRole.Member))
            .ToList();

    private static ChatRoomSummaryGetResponseDto MapToSummaryDto(ChatRoom room) =>
        new(room.Id, room.Kind, room.Title, room.PlatformGroupId, room.UpdatedAt);

    private async Task<ChatRoomGetResponseDto> MapToGetDtoAsync(ChatRoom room, bool includeParticipants)
    {
        var participants = includeParticipants
            ? room.Participants.ToList()
            : [];

        var nicknames = await _userService.GetNicknamesByUserIdsAsync(participants.Select(p => p.UserId));
        var participantDtos = participants
            .Select(p => MapParticipantToDto(p, nicknames.GetValueOrDefault(p.UserId, "Deleted User")))
            .ToList();

        return new ChatRoomGetResponseDto(
            room.Id,
            room.Kind,
            room.Title,
            room.PlatformGroupId,
            room.CreatedByUserId,
            room.CreatedAt,
            room.UpdatedAt,
            participantDtos);
    }

    private static ChatRoomParticipantGetResponseDto MapParticipantToDto(
        ChatRoomParticipant participant,
        string nickname) =>
        new(
            participant.Id,
            participant.UserId,
            nickname,
            participant.Role,
            participant.JoinedAt);
}
