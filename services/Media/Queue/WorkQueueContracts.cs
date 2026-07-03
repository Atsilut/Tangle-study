namespace Media.Global.Queue;

public sealed record MediaUploadedJob(
    long MediaAssetId,
    string IntendedContext,
    string Kind,
    string MimeType,
    string OriginalObjectKey,
    long OriginalSizeBytes,
    long TargetMaxBytes,
    int SchemaVersion = 1);
