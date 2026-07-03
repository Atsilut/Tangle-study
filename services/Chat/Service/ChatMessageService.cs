using Chat.Client;
using Chat.Config;
using Chat.Db;
using Chat.Dto;
using Chat.Entities;
using Chat.Events;
using Chat.Exceptions;
using Chat.Infrastructure;
using Chat.Queue;
using Chat.Realtime;
using Chat.Repository;
using Microsoft.Extensions.Options;

namespace Chat.Service;

[Service]
public class ChatMessageService(
    IChatMessageRepository repo,
    ChatDbContext db,
    ChatRoomService chatRoomService,
    IMonolithAccessClient monolithAccess,
    IMediaClient mediaClient,
    IChatRealtimeNotifier realtime,
    IEventPublisher eventPublisher,
    IWorkQueue workQueue,
    IHttpContextAccessor httpContextAccessor,
    IOptions<ChatMessagePolicyOptions> chatPolicy)
{
    public const int DefaultPageLimit = 50;
    public const int MaxPageLimit = 100;

    private readonly IChatMessageRepository _repo = repo;
    private readonly ChatDbContext _db = db;
    private readonly IMediaClient _mediaClient = mediaClient;
    private readonly ChatRoomService _chatRoomService = chatRoomService;
    private readonly IMonolithAccessClient _monolithAccess = monolithAccess;
    private readonly IChatRealtimeNotifier _realtime = realtime;
    private readonly IEventPublisher _eventPublisher = eventPublisher;
    private readonly IWorkQueue _workQueue = workQueue;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ChatMessagePolicyOptions _policy = chatPolicy.Value;

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

        return await MapManyAsync(messages, GetUserIdFromLogin());
    }

    public async Task MarkMessagesSeenAsync(long roomId, ChatMessageMarkSeenRequestDto request)
    {
        await _chatRoomService.EnsureCurrentUserIsParticipantAsync(roomId);

        if (request.MessageIds.Length == 0) return;

        var viewerUserId = GetUserIdFromLogin();
        var messageIds = request.MessageIds.Distinct().ToArray();
        var messages = await _repo.GetChatMessagesByIdsAsync(messageIds);

        foreach (var messageId in messageIds)
        {
            if (!messages.TryGetValue(messageId, out var message))
                throw new EntityNotFoundException("Message not found", StatusCodes.Status400BadRequest);

            if (message.ChatRoomId != roomId)
                throw new ArgumentException("Message does not belong to this chat room.");
        }

        await _repo.MarkMessagesSeenByUserAsync(viewerUserId, messageIds);
    }

    public async Task<ChatMessageGetResponseDto> CreateMessageAsync(long roomId, ChatMessageCreateRequestDto request)
    {
        await _chatRoomService.EnsureCurrentUserIsParticipantAsync(roomId);

        var senderUserId = GetUserIdFromLogin();
        await _monolithAccess.EnsureUserExistsAsync(senderUserId);

        var body = request.Body.Trim();
        if (body.Length == 0 && request.MediaAssetId is null)
            throw new ArgumentException("Message body cannot be empty.");

        var message = new ChatMessage(roomId, senderUserId, body);
        await _db.ExecuteInTransactionAsync(async () =>
        {
            await _repo.CreateChatMessageAsync(message);
            await _mediaClient.LinkToChatMessageAsync(message.Id, senderUserId, request.MediaAssetId);
        });
        await _chatRoomService.TouchRoomUpdatedAtAsync(roomId);

        var dto = await MapToDtoAsync(message, senderUserId);
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

    public async Task<IReadOnlyDictionary<long, ChatRoomSummaryLastMessageDto>> GetLatestMessageSummariesByRoomIdsAsync(
        IReadOnlyCollection<long> roomIds)
    {
        if (roomIds.Count == 0) return new Dictionary<long, ChatRoomSummaryLastMessageDto>();

        var messagesByRoom = await _repo.GetLatestChatMessagesByRoomIdsAsync(roomIds);
        if (messagesByRoom.Count == 0) return new Dictionary<long, ChatRoomSummaryLastMessageDto>();

        var messages = messagesByRoom.Values.ToList();
        var nicknames = await _monolithAccess.GetNicknamesByUserIdsAsync(messages.Select(m => m.LogicalSenderUserId));
        var mediaByMessageId = await _mediaClient.GetMediaByChatMessageIdsAsync([.. messages.Select(m => m.Id)]);

        return messages.ToDictionary(
            m => m.ChatRoomId,
            m => new ChatRoomSummaryLastMessageDto(
                m.LogicalSenderUserId,
                m.IsDeleted ? string.Empty : m.Body,
                nicknames.GetValueOrDefault(m.LogicalSenderUserId, "Deleted User"),
                m.SentAt,
                !m.IsDeleted && mediaByMessageId.GetValueOrDefault(m.Id) is not null));
    }

    public async Task<ChatMessageGetResponseDto> UpdateMessageAsync(
        long roomId,
        long messageId,
        ChatMessagePatchRequestDto request)
    {
        await _chatRoomService.EnsureCurrentUserIsParticipantAsync(roomId);

        var userId = GetUserIdFromLogin();
        var message = await _repo.GetChatMessageByIdAsync(messageId)
            ?? throw new EntityNotFoundException("Message not found");

        if (message.ChatRoomId != roomId) throw new ArgumentException("Message does not belong to this chat room.");
        if (message.SenderUserId != userId) throw new UnauthorizedAccessException("Unauthorized access");

        if (!ChatMessageEligibility.CanEdit(message, userId, _policy, DateTime.UtcNow))
            throw new ArgumentException("This message can no longer be edited.");

        var body = request.Body.Trim();
        if (body.Length == 0) throw new ArgumentException("Message body cannot be empty.");
        if (string.Equals(message.Body, body, StringComparison.Ordinal))
            return await MapToDtoAsync(message, userId);

        await _db.ExecuteInTransactionAsync(async () =>
        {
            _repo.AddChatMessageEdit(new ChatMessageEdit(messageId, message.Body));
            message.UpdateBody(body);
            await _repo.SaveChatMessageAsync(message);
        });
        await _chatRoomService.TouchRoomUpdatedAtAsync(roomId);

        var dto = await MapToDtoAsync(message, userId);
        await _realtime.NotifyMessageEditedAsync(roomId, dto);
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

        var seenByOther = await _repo.GetMessageIdsSeenByOtherParticipantsAsync([messageId]);
        if (!ChatMessageEligibility.CanDelete(
                message, userId, seenByOther.Contains(messageId), _policy, DateTime.UtcNow))
            throw new ArgumentException("This message can no longer be deleted.");

        await _db.ExecuteInTransactionAsync(async () =>
        {
            await _mediaClient.DeleteBlobStorageForChatMessageAsync(messageId);
            message.MarkDeleted();
            await _repo.SaveChatMessageAsync(message);
        });
        await _chatRoomService.TouchRoomUpdatedAtAsync(roomId);

        var dto = await MapToDtoAsync(message, userId);
        await _realtime.NotifyMessageDeletedAsync(roomId, dto);
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

        if (anchor.ChatRoomId != roomId)
            throw new ArgumentException("beforeMessageId must refer to a message in this chat room.");
    }

    private async Task<List<ChatMessageGetResponseDto>> MapManyAsync(
        IReadOnlyList<ChatMessage> messages,
        long viewerUserId)
    {
        var senderIds = messages.Select(m => m.LogicalSenderUserId).Distinct();
        var nicknames = await _monolithAccess.GetNicknamesByUserIdsAsync(senderIds);
        var mediaByMessageId = await _mediaClient.GetMediaByChatMessageIdsAsync([.. messages.Select(m => m.Id)]);
        var seenByOther = await _repo.GetMessageIdsSeenByOtherParticipantsAsync([.. messages.Select(m => m.Id)]);
        var editsByMessageId = await _repo.GetChatMessageEditsByMessageIdsAsync([.. messages.Select(m => m.Id)]);
        var utcNow = DateTime.UtcNow;
        return [.. messages
            .Select(m => MapToDto(
                m,
                nicknames.GetValueOrDefault(m.LogicalSenderUserId, "Deleted User"),
                m.IsDeleted ? null : mediaByMessageId.GetValueOrDefault(m.Id),
                BuildEditHistoryTree(editsByMessageId.GetValueOrDefault(m.Id)),
                viewerUserId,
                seenByOther.Contains(m.Id),
                utcNow))];
    }

    private async Task<ChatMessageGetResponseDto> MapToDtoAsync(ChatMessage message, long viewerUserId)
    {
        var nicknames = await _monolithAccess.GetNicknamesByUserIdsAsync([message.LogicalSenderUserId]);
        var media = message.IsDeleted
            ? null
            : await _mediaClient.GetMediaForChatMessageAsync(message.Id);
        var seenByOther = await _repo.GetMessageIdsSeenByOtherParticipantsAsync([message.Id]);
        var editsByMessageId = await _repo.GetChatMessageEditsByMessageIdsAsync([message.Id]);
        return MapToDto(
            message,
            nicknames.GetValueOrDefault(message.LogicalSenderUserId, "Deleted User"),
            media,
            BuildEditHistoryTree(editsByMessageId.GetValueOrDefault(message.Id)),
            viewerUserId,
            seenByOther.Contains(message.Id),
            DateTime.UtcNow);
    }

    private static ChatMessageEditGetResponseDto? BuildEditHistoryTree(IReadOnlyList<ChatMessageEdit>? edits)
    {
        if (edits is null or { Count: 0 }) return null;

        ChatMessageEditGetResponseDto? nested = null;
        foreach (var edit in edits.OrderBy(e => e.RecordedAt))
        {
            nested = new ChatMessageEditGetResponseDto(
                edit.Id,
                edit.Body,
                edit.RecordedAt,
                nested is null ? [] : [nested]);
        }

        return nested;
    }

    private ChatMessageGetResponseDto MapToDto(
        ChatMessage message,
        string senderNickname,
        MediaAssetGetResponseDto? media,
        ChatMessageEditGetResponseDto? editHistory,
        long viewerUserId,
        bool seenByOtherParticipant,
        DateTime utcNow) =>
        new(
            message.Id,
            message.ChatRoomId,
            message.LogicalSenderUserId,
            senderNickname,
            message.Body,
            message.SentAt,
            message.UpdatedAt,
            message.IsDeleted,
            !message.IsDeleted && editHistory is not null,
            ChatMessageEligibility.CanEdit(message, viewerUserId, _policy, utcNow),
            ChatMessageEligibility.CanDelete(message, viewerUserId, seenByOtherParticipant, _policy, utcNow),
            editHistory,
            media);
}
