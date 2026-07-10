using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Media.Entities;

public class MediaAsset
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public long? UploaderId { get; private set; }

    public long? DeletedUploaderId { get; private set; }

    public long LogicalUploaderId => UploaderId ?? DeletedUploaderId!.Value;

    public MediaKind Kind { get; private set; }
    public MediaIntendedContext IntendedContext { get; private set; }
    public MediaProcessingStatus ProcessingStatus { get; private set; }

    [MaxLength(255)]
    public string MimeType { get; private set; } = string.Empty;

    [MaxLength(512)]
    public string OriginalFileName { get; private set; } = string.Empty;

    [MaxLength(1024)]
    public string OriginalObjectKey { get; private set; } = string.Empty;

    [MaxLength(1024)]
    public string? ProcessedObjectKey { get; private set; }

    public long OriginalSizeBytes { get; private set; }
    public long? StoredSizeBytes { get; private set; }

    [MaxLength(2000)]
    public string? FailureReason { get; private set; }

    public long? PostId { get; private set; }

    public long? CommentId { get; private set; }

    public long? ChatMessageId { get; private set; }

    public bool IsLinked => PostId is not null || CommentId is not null || ChatMessageId is not null;

    private MediaAsset() { }

    public static MediaAsset CreatePendingUpload(
        long uploaderUserId,
        MediaIntendedContext intendedContext,
        MediaKind kind,
        string mimeType,
        string originalFileName,
        string originalObjectKey,
        long originalSizeBytes) =>
        new()
        {
            UploaderId = uploaderUserId,
            IntendedContext = intendedContext,
            Kind = kind,
            MimeType = mimeType,
            OriginalFileName = originalFileName,
            OriginalObjectKey = originalObjectKey,
            OriginalSizeBytes = originalSizeBytes,
            ProcessingStatus = MediaProcessingStatus.PendingUpload,
        };

    public void MarkProcessing()
    {
        ProcessingStatus = MediaProcessingStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkReady(string processedObjectKey, long storedSizeBytes)
    {
        ProcessedObjectKey = processedObjectKey;
        StoredSizeBytes = storedSizeBytes;
        ProcessingStatus = MediaProcessingStatus.Ready;
        FailureReason = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        ProcessingStatus = MediaProcessingStatus.Failed;
        FailureReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DetachUploader(long uploaderId)
    {
        DeletedUploaderId = uploaderId;
        UploaderId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LinkToPost(long postId)
    {
        EnsureCanLink(MediaIntendedContext.Post);
        PostId = postId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LinkToComment(long commentId)
    {
        EnsureCanLink(MediaIntendedContext.Comment);
        CommentId = commentId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LinkToChatMessage(long chatMessageId)
    {
        EnsureCanLink(MediaIntendedContext.ChatMessage);
        ChatMessageId = chatMessageId;
        UpdatedAt = DateTime.UtcNow;
    }

    private void EnsureCanLink(MediaIntendedContext expectedContext)
    {
        if (IntendedContext != expectedContext)
            throw new ArgumentException($"Media intended for {IntendedContext} cannot link to {expectedContext}.");
        if (IsLinked) throw new ArgumentException("Media is already linked to content.");
    }
}
