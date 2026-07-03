namespace Chat.Client;

public enum MediaKind
{
    Video = 0,
    Image = 1,
}

public enum MediaIntendedContext
{
    Post = 0,
    Comment = 1,
    ChatMessage = 2,
}

public enum MediaProcessingStatus
{
    PendingUpload = 0,
    Processing = 1,
    Ready = 2,
    Failed = 3,
}

public sealed record MediaAssetGetResponseDto(
    long Id,
    MediaKind Kind,
    MediaIntendedContext IntendedContext,
    MediaProcessingStatus ProcessingStatus,
    string MimeType,
    string OriginalFileName,
    long OriginalSizeBytes,
    long? StoredSizeBytes,
    string? FailureReason,
    long? PostId,
    long? CommentId,
    long? ChatMessageId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
