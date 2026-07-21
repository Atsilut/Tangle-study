using Community.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Community.Client;

internal sealed class HttpMediaClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<MediaClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpMediaClient)), IMediaClient
{
    public Task LinkToPostAsync(long postId, long uploaderUserId, IReadOnlyList<long>? mediaAssetIds) =>
        PostNoContentAsync("internal/media/link/post", content: new { postId, uploaderUserId, mediaAssetIds });

    public Task PatchPostMediaAsync(
        long postId,
        long uploaderUserId,
        IReadOnlyList<long>? addMediaAssetIds,
        IReadOnlyList<long>? removeMediaAssetIds) =>
        PostNoContentAsync(
            "internal/media/link/post/patch",
            content: new { postId, uploaderUserId, addMediaAssetIds, removeMediaAssetIds });

    public Task LinkToCommentAsync(long commentId, long uploaderUserId, long? mediaAssetId)
    {
        if (mediaAssetId is null) return Task.CompletedTask;

        return PostNoContentAsync(
            "internal/media/link/comment",
            content: new { commentId, uploaderUserId, mediaAssetId });
    }

    public Task UnlinkFromPostAsync(long postId) =>
        PostNoContentAsync($"internal/media/unlink/post/{postId}");

    public Task UnlinkFromCommentAsync(long commentId) =>
        PostNoContentAsync($"internal/media/unlink/comment/{commentId}");

    public async Task<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>> GetMediaByPostIdsAsync(
        IReadOnlyCollection<long> postIds)
    {
        var result = await PostJsonAsync<Dictionary<long, List<MediaAssetGetResponseDto>>>(
            "internal/media/batch/by-post-ids",
            new { postIds }) ?? [];

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MediaAssetGetResponseDto>)pair.Value);
    }

    public async Task<IReadOnlyList<MediaAssetGetResponseDto>> GetMediaForPostAsync(long postId)
    {
        var result = await GetMediaByPostIdsAsync([postId]);
        return result.GetValueOrDefault(postId) ?? [];
    }

    public async Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByCommentIdsAsync(
        IReadOnlyCollection<long> commentIds) =>
        await PostJsonAsync<Dictionary<long, MediaAssetGetResponseDto?>>(
            "internal/media/batch/by-comment-ids",
            new { commentIds }) ?? [];

    public Task DeleteBlobStorageForPostAsync(long postId) =>
        DeleteNoContentAsync($"internal/media/for-post/{postId}");

    public Task DeleteBlobStorageForPostsAsync(IReadOnlyCollection<long> postIds) =>
        DeleteNoContentAsync("internal/media/for-posts", content: new { postIds });

    public Task DeleteBlobStorageForCommentAsync(long commentId) =>
        DeleteNoContentAsync($"internal/media/for-comment/{commentId}");

    public Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds) =>
        DeleteNoContentAsync("internal/media/for-comments", content: new { commentIds });
}
