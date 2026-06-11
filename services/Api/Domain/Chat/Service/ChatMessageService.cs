using Api.Domain.Chat.Domain;
using Api.Domain.Chat.Dto;
using Api.Domain.Media.Dto;
using Api.Domain.Chat.Realtime;
using Api.Domain.Chat.Repository;
using Api.Domain.Media.Service;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Global.Events;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Api.Global.Queue;

namespace Api.Domain.Chat.Service;

[Service]
public class ChatMessageService(
    IChatMessageRepository repo,
    AppDbContext db,
    ChatRoomService chatRoomService,
    UserService userService,
    Lazy<MediaService> mediaService,
    IChatRealtimeNotifier realtime,
    IEventPublisher eventPublisher,
    IWorkQueue workQueue,
    IHttpContextAccessor httpContextAccessor)
{
    public const int DefaultPageLimit = 50;
    public const int MaxPageLimit = 100;

    private readonly IChatMessageRepository _repo = repo;
    private readonly AppDbContext _db = db;
    private readonly Lazy<MediaService> _mediaService = mediaService;
    private readonly ChatRoomService _chatRoomService = chatRoomService;
    private readonly UserService _userService = userService;
    private readonly IChatRealtimeNotifier _realtime = realtime;
    private readonly IEventPublisher _eventPublisher = eventPublisher;
    private readonly IWorkQueue _workQueue = workQueue;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));

    public async Task<List<ChatMessageGetResponseDto>?> GetMessagesForRoomAsync(
        long roomId,
        long? beforeMessageId,
        int? limit)
    {
        await _chatRoomService.EnsureCurrentUserIsParticipantAsync(roomId);

        var pageLimit = NormalizeLimit(limit);
        if (beforeMessageId is not null) await EnsureBeforeMessageBelongsToRoomAsync(roomId, beforeMessageId.Value);

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

        var body = request.Body.Trim();
        if (body.Length == 0 && request.MediaAssetId is null)
            throw new ArgumentException("Message body cannot be empty.");

        var message = new ChatMessage(roomId, senderUserId, body);
        await _db.ExecuteInTransactionAsync(async () =>
        {
            await _repo.CreateChatMessageAsync(message);
            await _mediaService.Value.LinkToChatMessageAsync(message.Id, senderUserId, request.MediaAssetId);
        });
        await _chatRoomService.TouchRoomUpdatedAtAsync(roomId);

        var dto = await MapToDtoAsync(message);
        await _realtime.NotifyMessageCreatedAsync(roomId, dto);
        await _eventPublisher.PublishAsync(
            RedisEventChannels.ChatMessageCreated,
            new ChatMessageCreatedEvent(
                message.Id,
                message.ChatRoomId,
                senderUserId,
                message.Body,
                message.SentAt));
        await _workQueue.EnqueueAsync(
            WorkQueueStreams.ChatMessageCreated,
            new ChatMessageCreatedJob(
                message.Id,
                message.ChatRoomId,
                senderUserId,
                message.Body,
                message.SentAt));
        return dto;
    }

    public Task DetachSenderFromDeletedUserAsync(long userId) =>
        _repo.DetachSenderFromMessagesAsync(userId);

    public async Task EnsureCurrentUserCanAccessMessageAsync(long messageId)
    {
        var message = await _repo.GetChatMessageByIdAsync(messageId)
            ?? throw new EntityNotFoundException("Message not found");
        await _chatRoomService.EnsureCurrentUserIsParticipantAsync(message.ChatRoomId);
    }

    public async Task DeleteMessageAsync(long roomId, long messageId)
    {
        await _chatRoomService.EnsureCurrentUserIsParticipantAsync(roomId);

        var userId = GetUserIdFromLogin();
        var message = await _repo.GetChatMessageByIdAsync(messageId)
            ?? throw new EntityNotFoundException("Message not found");

        if (message.ChatRoomId != roomId) throw new ArgumentException("Message does not belong to this chat room.");
        if (message.SenderUserId != userId) throw new UnauthorizedAccessException("Unauthorized access");

        await _db.ExecuteInTransactionAsync(async () =>
        {
            await _mediaService.Value.DeleteBlobStorageForChatMessageAsync(messageId);
            await _repo.DeleteChatMessageAsync(message);
        });
    }

    private static int NormalizeLimit(int? limit)
    {
        if (limit is null or < 1) return DefaultPageLimit;
        return Math.Min(limit.Value, MaxPageLimit);
    }

    private async Task EnsureBeforeMessageBelongsToRoomAsync(long roomId, long beforeMessageId)
    {
        var anchor = await _repo.GetChatMessageByIdAsync(beforeMessageId)
            ?? throw new EntityNotFoundException("Message not found", StatusCodes.Status400BadRequest);

        if (anchor.ChatRoomId != roomId) throw new ArgumentException("beforeMessageId must refer to a message in this chat room.");
    }

    private async Task<List<ChatMessageGetResponseDto>> MapManyAsync(IReadOnlyList<ChatMessage> messages)
    {
        var senderIds = messages.Select(m => m.LogicalSenderUserId).Distinct();
        var nicknames = await _userService.GetNicknamesByUserIdsAsync(senderIds);
        var mediaByMessageId = await _mediaService.Value.GetMediaByChatMessageIdsAsync([.. messages.Select(m => m.Id)]);
        return [.. messages
            .Select(m => MapToDto(
                m,
                nicknames.GetValueOrDefault(m.LogicalSenderUserId, "Deleted User"),
                mediaByMessageId.GetValueOrDefault(m.Id)))];
    }

    private async Task<ChatMessageGetResponseDto> MapToDtoAsync(ChatMessage message)
    {
        var nicknames = await _userService.GetNicknamesByUserIdsAsync([message.LogicalSenderUserId]);
        var media = await _mediaService.Value.GetMediaForChatMessageAsync(message.Id);
        return MapToDto(
            message,
            nicknames.GetValueOrDefault(message.LogicalSenderUserId, "Deleted User"),
            media);
    }

    private static ChatMessageGetResponseDto MapToDto(
        ChatMessage message,
        string senderNickname,
        MediaAssetGetResponseDto? media) =>
        new(
            message.Id,
            message.ChatRoomId,
            message.LogicalSenderUserId,
            senderNickname,
            message.Body,
            message.SentAt,
            media);
}
