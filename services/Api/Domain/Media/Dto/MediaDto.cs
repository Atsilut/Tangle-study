using Api.Domain.Media.Domain;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace Api.Domain.Media.Dto;

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

public sealed record MediaUploadInitRequestDto
{
    [Required]
    [SwaggerSchema(Description = "Target content type for this upload")]
    public required MediaIntendedContext IntendedContext { get; init; }

    [Required]
    [StringLength(255, MinimumLength = 3)]
    [SwaggerSchema(Description = "MIME type (image/* or video/*)")]
    public required string MimeType { get; init; }

    [Required]
    [StringLength(512, MinimumLength = 1)]
    [SwaggerSchema(Description = "Original file name")]
    public required string FileName { get; init; }

    [Range(1, long.MaxValue)]
    [SwaggerSchema(Description = "Declared raw file size in bytes")]
    public required long SizeBytes { get; init; }
}

public sealed record MediaUploadInitResponseDto(
    long MediaAssetId,
    string UploadUrl,
    string ObjectKey,
    DateTime ExpiresAt,
    long IngressLimitBytes,
    long StorageLimitBytes);

public sealed record MediaContentResult(Stream Stream, string ContentType, string FileName);

public sealed record MediaProcessedRequestDto
{
    [StringLength(1024)]
    [SwaggerSchema(Description = "Blob key for the processed object; required on success")]
    public string? ProcessedObjectKey { get; init; }

    [Range(1, long.MaxValue)]
    [SwaggerSchema(Description = "Processed file size in bytes; required on success")]
    public long? StoredSizeBytes { get; init; }

    [StringLength(2000)]
    [SwaggerSchema(Description = "When set, marks the asset as failed instead of ready")]
    public string? FailureReason { get; init; }
}
