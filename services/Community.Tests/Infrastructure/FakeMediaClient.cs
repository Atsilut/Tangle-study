using Community.Client;

namespace Community.Tests.Infrastructure;

public sealed class FakeMediaClient : IMediaClient
{
    private long _nextId = 1;
    private readonly Dictionary<long, AssetState> _assets = [];
    private Exception? _linkFailure;
    private Exception? _deletePostBlobFailure;
    private Exception? _deleteCommentBlobFailure;

    private sealed class AssetState
    {
        public required MediaAssetGetResponseDto Dto { get; set; }
    }

    public void Reset()
    {
        _assets.Clear();
        _nextId = 1;
        _linkFailure = null;
        _deletePostBlobFailure = null;
        _deleteCommentBlobFailure = null;
    }

    public void FailNextLink(Exception exception) => _linkFailure = exception;

    public void FailNextDeleteBlobForPost(Exception exception) => _deletePostBlobFailure = exception;

    public void FailNextDeleteBlobForComment(Exception exception) => _deleteCommentBlobFailure = exception;

    public bool IsAssetLinkedToPost(long mediaAssetId) =>
        _assets.TryGetValue(mediaAssetId, out var state) && state.Dto.PostId is not null;

    public bool IsAssetLinkedToComment(long mediaAssetId) =>
        _assets.TryGetValue(mediaAssetId, out var state) && state.Dto.CommentId is not null;

    public bool HasAnyAssets => _assets.Count > 0;

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
        if (_linkFailure is not null)
        {
            var failure = _linkFailure;
            _linkFailure = null;
            throw failure;
        }

        if (mediaAssetIds is null) return Task.CompletedTask;
        foreach (var id in mediaAssetIds)
        {
            if (!_assets.TryGetValue(id, out var state))
                throw new InvalidOperationException($"Fake media asset {id} was not seeded.");
            if (state.Dto.PostId == postId) continue;
            if (state.Dto.PostId is not null || state.Dto.CommentId is not null || state.Dto.ChatMessageId is not null)
                throw new ArgumentException("Media is already linked to content.");
            state.Dto = state.Dto with { PostId = postId };
        }

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
        if (_linkFailure is not null)
        {
            var failure = _linkFailure;
            _linkFailure = null;
            throw failure;
        }

        if (mediaAssetId is long id)
        {
            if (!_assets.TryGetValue(id, out var state))
                throw new InvalidOperationException($"Fake media asset {id} was not seeded.");
            if (state.Dto.CommentId == commentId) return Task.CompletedTask;
            if (state.Dto.PostId is not null || state.Dto.CommentId is not null || state.Dto.ChatMessageId is not null)
                throw new ArgumentException("Media is already linked to content.");
            state.Dto = state.Dto with { CommentId = commentId };
        }

        return Task.CompletedTask;
    }

    public Task UnlinkFromPostAsync(long postId)
    {
        foreach (var state in _assets.Values.Where(a => a.Dto.PostId == postId).ToList())
            state.Dto = state.Dto with { PostId = null };
        return Task.CompletedTask;
    }

    public Task UnlinkFromCommentAsync(long commentId)
    {
        foreach (var state in _assets.Values.Where(a => a.Dto.CommentId == commentId).ToList())
            state.Dto = state.Dto with { CommentId = null };
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
        if (_deletePostBlobFailure is not null)
        {
            var failure = _deletePostBlobFailure;
            _deletePostBlobFailure = null;
            throw failure;
        }

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
        if (_deleteCommentBlobFailure is not null)
        {
            var failure = _deleteCommentBlobFailure;
            _deleteCommentBlobFailure = null;
            throw failure;
        }

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
