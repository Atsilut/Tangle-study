namespace Chat.Events;

public sealed record ChatMessageCreatedEvent(
    long MessageId,
    long ChatRoomId,
    long SenderUserId,
    string Body,
    DateTimeOffset SentAt,
    int SchemaVersion = 1);
