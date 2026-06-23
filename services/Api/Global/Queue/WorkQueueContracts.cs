namespace Api.Global.Queue;

public sealed record ChatMessageCreatedJob(
    long MessageId,
    long ChatRoomId,
    long SenderUserId,
    string Body,
    DateTimeOffset SentAt,
    int SchemaVersion = 1);

public sealed record MediaUploadedJob(
    long MediaAssetId,
    string IntendedContext,
    string Kind,
    string MimeType,
    string OriginalObjectKey,
    long OriginalSizeBytes,
    long TargetMaxBytes,
    int SchemaVersion = 1);

public sealed record LocationClusterJob(
    decimal MinLatitude,
    decimal MaxLatitude,
    decimal MinLongitude,
    decimal MaxLongitude,
    int Zoom,
    int SchemaVersion = 1);
