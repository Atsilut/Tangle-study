using Api.Domain.Chat.Domain;
using Api.Domain.Chat.Dto;
using Api.Domain.Chat.Realtime;
using Api.Domain.Chat.Repository;
using Api.Domain.Users.Service;
using Api.Global.Events;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Chat.Service;

[Service]
public class ChatMessageService
{
    public const int DefaultPageLimit = 50;
    public const int MaxPageLimit = 100;

    private readonly IChatMessageRepository _repo;
    private readonly ChatRoomService _chatRoomService;
    private readonly UserService _userService;
    private readonly IChatRealtimeNotifier _realtime;
    private readonly IEventPublisher _eventPublisher;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ChatMessageService(
        IChatMessageRepository repo,
        ChatRoomService chatRoomService,
        UserService userService,
        IChatRealtimeNotifier realtime,
        IEventPublisher eventPublisher,
        IHttpContextAccessor httpContextAccessor)
    {
        _repo = repo;
        _chatRoomService = chatRoomService;
        _userService = userService;
        _realtime = realtime;
        _eventPublisher = eventPublisher;
        _httpContextAccessor = httpContextAccessor;
    }

    private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));

    public async Task<List<ChatMessageGetResponseDto>?> GetMessagesForRoomAsync(
        long roomId,
        long? beforeMessageId,
        int? limit)
    {
        await _chatRoomService.EnsureCurrentUserIsParticipantAsync(roomId);

        var pageLimit = NormalizeLimit(limit);
        if (beforeMessageId is not null)
            await EnsureBeforeMessageBelongsToRoomAsync(roomId, beforeMessageId.Value);

        var messages = await _repo.GetChatMessagesForRoomAsync(roomId, beforeMessageId, pageLimit);
        if (messages.Count == 0) return null;

        return await MapManyAsync(messages);
    }

    public async Task<ChatMessageGetResponseDto> CreateMessageAsync(long roomId, ChatMessageCreateRequestDto request)
    {
        await _chatRoomService.EnsureCurrentUserIsParticipantAsync(roomId);

        var senderUserId = GetUserIdFromLogin();
        await _userService.EnsureUserExistsAsync(
            senderUserId,
            "Authentication failed",
            StatusCodes.Status400BadRequest);

        var message = new ChatMessage(roomId, senderUserId, request.Body);
        await _repo.CreateChatMessageAsync(message);
        await _chatRoomService.TouchRoomUpdatedAtAsync(roomId);

        var dto = await MapToDtoAsync(message);
        await _realtime.NotifyMessageCreatedAsync(roomId, dto);
        await _eventPublisher.PublishAsync(
            RedisEventChannels.ChatMessageCreated,
            new ChatMessageCreatedEvent(
                message.Id,
                message.ChatRoomId,
                message.SenderUserId,
                message.Body,
                message.SentAt));
        return dto;
    }

    private static int NormalizeLimit(int? limit)
    {
        if (limit is null or < 1)
            return DefaultPageLimit;
        return Math.Min(limit.Value, MaxPageLimit);
    }

    private async Task EnsureBeforeMessageBelongsToRoomAsync(long roomId, long beforeMessageId)
    {
        var anchor = await _repo.GetChatMessageByIdAsync(beforeMessageId)
            ?? throw new EntityNotFoundException("Message not found", StatusCodes.Status400BadRequest);

        if (anchor.ChatRoomId != roomId)
            throw new ArgumentException("beforeMessageId must refer to a message in this chat room.");
    }

    private async Task<ChatMessageGetResponseDto> MapToDtoAsync(ChatMessage message)
    {
        var nicknames = await _userService.GetNicknamesByUserIdsAsync([message.SenderUserId]);
        return MapToDto(message, nicknames.GetValueOrDefault(message.SenderUserId, "Deleted User"));
    }

    private async Task<List<ChatMessageGetResponseDto>> MapManyAsync(IReadOnlyList<ChatMessage> messages)
    {
        var senderIds = messages.Select(m => m.SenderUserId).Distinct();
        var nicknames = await _userService.GetNicknamesByUserIdsAsync(senderIds);
        return messages
            .Select(m => MapToDto(m, nicknames.GetValueOrDefault(m.SenderUserId, "Deleted User")))
            .ToList();
    }

    private static ChatMessageGetResponseDto MapToDto(ChatMessage message, string senderNickname) =>
        new(
            message.Id,
            message.ChatRoomId,
            message.SenderUserId,
            senderNickname,
            message.Body,
            message.SentAt);
}
