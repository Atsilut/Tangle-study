namespace Api.Global.Events;

public sealed record ChatMessageCreatedEvent(
    long MessageId,
    long ChatRoomId,
    long SenderUserId,
    string Body,
    DateTimeOffset SentAt,
    int SchemaVersion = 1);

public sealed record UserNicknameChangedEvent(
    long UserId,
    string? Nickname,
    bool IsDeleted,
    DateTimeOffset OccurredAt,
    int SchemaVersion = 1);
