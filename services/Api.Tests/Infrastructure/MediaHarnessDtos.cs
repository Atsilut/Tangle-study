using System.ComponentModel.DataAnnotations;
using Api.Client;

namespace Api.Tests.Infrastructure;

internal sealed record MediaUploadInitRequestDto
{
    [Required]
    public required MediaIntendedContext IntendedContext { get; init; }

    [Required]
    public required string MimeType { get; init; }

    [Required]
    public required string FileName { get; init; }

    [Required]
    public required long SizeBytes { get; init; }
}

internal sealed record MediaUploadInitResponseDto(
    long MediaAssetId,
    string UploadUrl,
    string ObjectKey,
    DateTime ExpiresAt,
    long IngressLimitBytes,
    long StorageLimitBytes);
