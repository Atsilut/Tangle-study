using Users.Client;

namespace Users.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="IMediaClient"/> for Users integration tests (no media-service container).
/// </summary>
public sealed class FakeMediaClient : IMediaClient
{
    private long _nextId = 1;
    private readonly Dictionary<long, AssetState> _assets = [];

    private sealed class AssetState
    {
        public required MediaAssetGetResponseDto Dto { get; set; }
    }

    public long SeedReadyAsset(
        MediaIntendedContext context,
        string mimeType,
        string fileName,
        long storedSizeBytes,
        long uploaderUserId = 1)
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

    public Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId)
    {
        if (mediaAssetId is long id)
            UpdateAsset(id, dto => dto with { ChatMessageId = chatMessageId });
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

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByChatMessageIdsAsync(
        IReadOnlyCollection<long> chatMessageIds)
    {
        var result = chatMessageIds.ToDictionary(
            messageId => messageId,
            messageId => _assets.Values
                .Select(a => a.Dto)
                .FirstOrDefault(d => d.ChatMessageId == messageId));
        return Task.FromResult<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>>(result);
    }

    public Task<MediaAssetGetResponseDto?> GetMediaForChatMessageAsync(long chatMessageId) =>
        Task.FromResult(_assets.Values
            .Select(a => a.Dto)
            .FirstOrDefault(d => d.ChatMessageId == chatMessageId));

    public Task DeleteBlobStorageForPostAsync(long postId) => Task.CompletedTask;

    public Task DeleteBlobStorageForPostsAsync(IReadOnlyCollection<long> postIds) => Task.CompletedTask;

    public Task DeleteBlobStorageForCommentAsync(long commentId) => Task.CompletedTask;

    public Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds) => Task.CompletedTask;

    public Task DeleteBlobStorageForChatMessageAsync(long chatMessageId) => Task.CompletedTask;

    public Task DetachUploaderFromDeletedUserAsync(long uploaderId)
    {
        DetachedUploaderIds.Add(uploaderId);
        return Task.CompletedTask;
    }

    public List<long> DetachedUploaderIds { get; } = [];

    private void UpdateAsset(long id, Func<MediaAssetGetResponseDto, MediaAssetGetResponseDto> update)
    {
        if (!_assets.TryGetValue(id, out var state))
            throw new InvalidOperationException($"Fake media asset {id} was not seeded.");
        state.Dto = update(state.Dto);
    }
}
