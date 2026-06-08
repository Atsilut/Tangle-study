using Api.Domain.Chat.Domain;
using Api.Domain.Friendships.Service;
using Api.Domain.Groups.Service;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Chat.Service;

[Service]
public class ChatRoomAccessService(
    FriendshipService friendshipService,
    UserBlockService userBlockService,
    UserService userService,
    GroupMembershipService groupMembershipService,
    GroupService groupService)
{
    private readonly FriendshipService _friendshipService = friendshipService;
    private readonly UserBlockService _userBlockService = userBlockService;
    private readonly UserService _userService = userService;
    private readonly GroupMembershipService _groupMembershipService = groupMembershipService;
    private readonly GroupService _groupService = groupService;

    public async Task EnsureNoBlockBetweenUsersAsync(long userId, long otherUserId)
    {
        if (await _userBlockService.IsBlockedByAsync(userId, otherUserId)
            || await _userBlockService.IsBlockedByAsync(otherUserId, userId))
            throw new ArgumentException("Cannot chat while a block exists between you and this user.");
    }

    public async Task EnsureCanCreateDirectRoomAsync(long userId, long otherUserId)
    {
        if (userId == otherUserId) throw new ArgumentException("Cannot create a direct chat room with yourself.");

        await _userService.EnsureUserExistsAsync(otherUserId, "User not found", StatusCodes.Status400BadRequest);
        await _friendshipService.EnsureFriendshipExistsForUserPairAsync(userId, otherUserId);
        await EnsureNoBlockBetweenUsersAsync(userId, otherUserId);
    }

    public void EnsureParticipantInRoom(IReadOnlyList<ChatRoomParticipant> participants, long userId)
    {
        if (!participants.Any(p => p.UserId == userId)) throw new UnauthorizedAccessException("Unauthorized access");
    }

    public void EnsureRoomOwner(IReadOnlyList<ChatRoomParticipant> participants, long userId)
    {
        var participant = participants.FirstOrDefault(p => p.UserId == userId)
            ?? throw new EntityNotFoundException("Chat room not found");
        if (!participant.IsOwner) throw new UnauthorizedAccessException("Unauthorized access");
    }

    public void EnsureCanAddParticipant(ChatRoom room, IReadOnlyList<ChatRoomParticipant> participants, long actorUserId)
    {
        if (room.HasRoomOwner) EnsureRoomOwner(participants, actorUserId);
        else EnsureParticipantInRoom(participants, actorUserId);
    }

    public async Task EnsureInviteeCanBeAddedAsync(
        ChatRoom room,
        long inviteeUserId,
        IReadOnlyList<ChatRoomParticipant> participants)
    {
        await _userService.EnsureUserExistsAsync(inviteeUserId, "User not found", StatusCodes.Status400BadRequest);

        await _userBlockService.EnsureNoBlockBetweenUserAndOthersAsync(
            inviteeUserId,
            participants.Select(p => p.UserId).ToList());

        if (room.Kind == ChatRoomKind.PlatformGroup)
        {
            var platformGroupId = room.PlatformGroupId
                ?? throw new InvalidOperationException("Platform group room is missing PlatformGroupId.");
            await _groupMembershipService.EnsureMemberAsync(
                platformGroupId,
                inviteeUserId,
                "User is not a member of this group");
        }
    }

    public async Task EnsureGroupMemberCanListRoomsAsync(long platformGroupId, long userId)
    {
        await _groupService.EnsureGroupExistsAsync(platformGroupId);
        await _groupMembershipService.EnsureMemberAsync(platformGroupId, userId);
    }

    public async Task EnsureCanCreatePlatformGroupRoomAsync(
        long platformGroupId,
        long creatorUserId,
        IReadOnlyCollection<long> participantUserIds)
    {
        await _groupService.EnsureGroupExistsAsync(platformGroupId);
        await _groupMembershipService.EnsureMemberAsync(platformGroupId, creatorUserId);

        var otherParticipantIds = OtherParticipantIds(creatorUserId, participantUserIds);
        if (otherParticipantIds.Count == 0) return;

        await _userService.EnsureUsersExistAsync(otherParticipantIds, "User not found", StatusCodes.Status400BadRequest);
        await _userBlockService.EnsureNoBlockBetweenUserAndOthersAsync(creatorUserId, otherParticipantIds);
        await _groupMembershipService.EnsureMembersAsync(
            platformGroupId,
            otherParticipantIds,
            "All participants must be members of this group");
    }

    public async Task EnsureCanCreateMultiRoomAsync(long creatorUserId, IReadOnlyCollection<long> participantUserIds)
    {
        var otherParticipantIds = OtherParticipantIds(creatorUserId, participantUserIds);
        if (otherParticipantIds.Count == 0) return;

        await _userService.EnsureUsersExistAsync(otherParticipantIds, "User not found", StatusCodes.Status400BadRequest);
        await _userBlockService.EnsureNoBlockBetweenUserAndOthersAsync(creatorUserId, otherParticipantIds);
    }

    private static List<long> OtherParticipantIds(long creatorUserId, IReadOnlyCollection<long> participantUserIds) =>
        [.. participantUserIds.Where(id => id != creatorUserId).Distinct()];
}
