namespace Api.Global.Queue;

public sealed record ChatMessageCreatedJob(
    long MessageId,
    long ChatRoomId,
    long SenderUserId,
    string Body,
    DateTimeOffset SentAt);

public sealed record MediaUploadedJob(
    long MediaAssetId,
    string IntendedContext,
    string Kind,
    string MimeType,
    string OriginalObjectKey,
    long OriginalSizeBytes,
    long TargetMaxBytes);

public sealed record LocationClusterJob(
    decimal MinLatitude,
    decimal MaxLatitude,
    decimal MinLongitude,
    decimal MaxLongitude,
    int Zoom);
