namespace Api.Domain.Media.Storage;

public sealed record PresignedUpload(string Url, string ObjectKey, DateTime ExpiresAt);

public interface IMediaStorage
{
    Task<PresignedUpload> CreatePresignedUploadAsync(
        string objectKey,
        string contentType,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);

    Task<bool> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken = default);

    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default);
}
