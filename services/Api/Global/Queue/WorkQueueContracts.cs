namespace Api.Global.Queue;

public sealed record ChatMessageCreatedJob(
    long MessageId,
    long ChatRoomId,
    long SenderUserId,
    string Body,
    DateTimeOffset SentAt);
