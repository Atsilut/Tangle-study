using Media.Storage;

namespace Media.Tests.Infrastructure;

public sealed class FakeMediaStorage : IMediaStorage
{
    private readonly HashSet<string> _objects = new(StringComparer.Ordinal);
    private readonly List<string> _deletedObjectKeys = [];

    public Task<PresignedUpload> CreatePresignedUploadAsync(
        string objectKey,
        string contentType,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        _objects.Add(objectKey);
        return Task.FromResult(new PresignedUpload($"https://fake/{objectKey}", objectKey, DateTime.UtcNow.Add(expiry)));
    }

    public Task<bool> ObjectExistsAsync(string objectKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_objects.Contains(objectKey));

    public Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (!_objects.Contains(objectKey))
            throw new FileNotFoundException($"Object not found: {objectKey}");

        return Task.FromResult<Stream>(new MemoryStream([]));
    }

    public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        _objects.Remove(objectKey);
        _deletedObjectKeys.Add(objectKey);
        return Task.CompletedTask;
    }

    public void SeedObject(string objectKey) => _objects.Add(objectKey);

    public void RemoveObject(string objectKey) => _objects.Remove(objectKey);

    public IReadOnlyList<string> GetDeletedObjectKeys() => _deletedObjectKeys;
}
