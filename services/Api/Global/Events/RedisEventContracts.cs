namespace Api.Global.Events;

public sealed record ChatMessageCreatedEvent(
    long MessageId,
    long ChatRoomId,
    long SenderUserId,
    string Body,
    DateTimeOffset SentAt);

public sealed record UserNicknameChangedEvent(
    long UserId,
    string? Nickname,
    bool IsDeleted,
    DateTimeOffset OccurredAt);
