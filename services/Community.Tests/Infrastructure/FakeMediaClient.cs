using Community.Client;

namespace Community.Tests.Infrastructure;

public sealed class FakeMediaClient : IMediaClient
{
    private long _nextId = 1;
    private readonly Dictionary<long, AssetState> _assets = [];

    private sealed class AssetState
    {
        public required MediaAssetGetResponseDto Dto { get; set; }
    }

    public void Reset()
    {
        _assets.Clear();
        _nextId = 1;
    }

    public long SeedReadyAsset(
        MediaIntendedContext context = MediaIntendedContext.Post,
        string mimeType = "image/png",
        string fileName = "photo.png",
        long storedSizeBytes = 1024)
    {
        var id = _nextId++;
        var kind = mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            ? MediaKind.Video
            : MediaKind.Image;
        var now = DateTime.UtcNow;
        _assets[id] = new AssetState
        {
            Dto = new MediaAssetGetResponseDto(
                id,
                kind,
                context,
                MediaProcessingStatus.Ready,
                mimeType,
                fileName,
                storedSizeBytes,
                storedSizeBytes,
                null,
                null,
                null,
                null,
                now,
                now),
        };
        return id;
    }

    public Task LinkToPostAsync(long postId, long uploaderUserId, IReadOnlyList<long>? mediaAssetIds)
    {
        if (mediaAssetIds is null) return Task.CompletedTask;
        foreach (var id in mediaAssetIds)
            UpdateAsset(id, dto => dto with { PostId = postId });
        return Task.CompletedTask;
    }

    public Task PatchPostMediaAsync(
        long postId,
        long uploaderUserId,
        IReadOnlyList<long>? addMediaAssetIds,
        IReadOnlyList<long>? removeMediaAssetIds)
    {
        if (addMediaAssetIds is not null)
        {
            foreach (var id in addMediaAssetIds)
                UpdateAsset(id, dto => dto with { PostId = postId });
        }

        if (removeMediaAssetIds is not null)
        {
            foreach (var id in removeMediaAssetIds)
                UpdateAsset(id, dto => dto with { PostId = null });
        }

        return Task.CompletedTask;
    }

    public Task LinkToCommentAsync(long commentId, long uploaderUserId, long? mediaAssetId)
    {
        if (mediaAssetId is long id)
            UpdateAsset(id, dto => dto with { CommentId = commentId });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>> GetMediaByPostIdsAsync(
        IReadOnlyCollection<long> postIds)
    {
        var result = postIds.ToDictionary(
            postId => postId,
            postId => (IReadOnlyList<MediaAssetGetResponseDto>)[.. _assets.Values
                .Select(a => a.Dto)
                .Where(d => d.PostId == postId)]);
        return Task.FromResult<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>>(result);
    }

    public Task<IReadOnlyList<MediaAssetGetResponseDto>> GetMediaForPostAsync(long postId) =>
        Task.FromResult<IReadOnlyList<MediaAssetGetResponseDto>>([.. _assets.Values
            .Select(a => a.Dto)
            .Where(d => d.PostId == postId)]);

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByCommentIdsAsync(
        IReadOnlyCollection<long> commentIds)
    {
        var result = commentIds.ToDictionary(
            commentId => commentId,
            commentId => _assets.Values
                .Select(a => a.Dto)
                .FirstOrDefault(d => d.CommentId == commentId));
        return Task.FromResult<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>>(result);
    }

    public Task DeleteBlobStorageForPostAsync(long postId)
    {
        RemoveWhere(dto => dto.PostId == postId);
        return Task.CompletedTask;
    }

    public Task DeleteBlobStorageForPostsAsync(IReadOnlyCollection<long> postIds)
    {
        var ids = postIds.ToHashSet();
        RemoveWhere(dto => dto.PostId is long postId && ids.Contains(postId));
        return Task.CompletedTask;
    }

    public Task DeleteBlobStorageForCommentAsync(long commentId)
    {
        RemoveWhere(dto => dto.CommentId == commentId);
        return Task.CompletedTask;
    }

    public Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds)
    {
        var ids = commentIds.ToHashSet();
        RemoveWhere(dto => dto.CommentId is long commentId && ids.Contains(commentId));
        return Task.CompletedTask;
    }

    private void RemoveWhere(Func<MediaAssetGetResponseDto, bool> predicate)
    {
        foreach (var id in _assets.Where(kv => predicate(kv.Value.Dto)).Select(kv => kv.Key).ToList())
            _assets.Remove(id);
    }

    private void UpdateAsset(long id, Func<MediaAssetGetResponseDto, MediaAssetGetResponseDto> update)
    {
        if (!_assets.TryGetValue(id, out var state))
            throw new InvalidOperationException($"Fake media asset {id} was not seeded.");
        state.Dto = update(state.Dto);
    }
}
