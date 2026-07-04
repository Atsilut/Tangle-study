using Community.Client;

namespace Community.Tests.Infrastructure;

public sealed class FakeMediaClient : IMediaClient
{
    public Task LinkToPostAsync(long postId, long uploaderUserId, IReadOnlyList<long>? mediaAssetIds) =>
        Task.CompletedTask;

    public Task PatchPostMediaAsync(
        long postId,
        long uploaderUserId,
        IReadOnlyList<long>? addMediaAssetIds,
        IReadOnlyList<long>? removeMediaAssetIds) =>
        Task.CompletedTask;

    public Task LinkToCommentAsync(long commentId, long uploaderUserId, long? mediaAssetId) =>
        Task.CompletedTask;

    public Task<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>> GetMediaByPostIdsAsync(
        IReadOnlyCollection<long> postIds) =>
        Task.FromResult<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>>(
            postIds.ToDictionary(id => id, _ => (IReadOnlyList<MediaAssetGetResponseDto>)[]));

    public Task<IReadOnlyList<MediaAssetGetResponseDto>> GetMediaForPostAsync(long postId) =>
        Task.FromResult<IReadOnlyList<MediaAssetGetResponseDto>>([]);

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByCommentIdsAsync(
        IReadOnlyCollection<long> commentIds) =>
        Task.FromResult<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>>(
            commentIds.ToDictionary(id => id, _ => (MediaAssetGetResponseDto?)null));

    public Task DeleteBlobStorageForPostAsync(long postId) => Task.CompletedTask;

    public Task DeleteBlobStorageForPostsAsync(IReadOnlyCollection<long> postIds) => Task.CompletedTask;

    public Task DeleteBlobStorageForCommentAsync(long commentId) => Task.CompletedTask;

    public Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds) => Task.CompletedTask;
}
