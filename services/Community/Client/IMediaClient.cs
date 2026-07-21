namespace Community.Client;

public interface IMediaClient
{
    public Task LinkToPostAsync(long postId, long uploaderUserId, IReadOnlyList<long>? mediaAssetIds);

    public Task PatchPostMediaAsync(
        long postId,
        long uploaderUserId,
        IReadOnlyList<long>? addMediaAssetIds,
        IReadOnlyList<long>? removeMediaAssetIds);

    public Task LinkToCommentAsync(long commentId, long uploaderUserId, long? mediaAssetId);

    public Task UnlinkFromPostAsync(long postId);

    public Task UnlinkFromCommentAsync(long commentId);

    public Task<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>> GetMediaByPostIdsAsync(
        IReadOnlyCollection<long> postIds);

    public Task<IReadOnlyList<MediaAssetGetResponseDto>> GetMediaForPostAsync(long postId);

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByCommentIdsAsync(
        IReadOnlyCollection<long> commentIds);

    public Task DeleteBlobStorageForPostAsync(long postId);

    public Task DeleteBlobStorageForPostsAsync(IReadOnlyCollection<long> postIds);

    public Task DeleteBlobStorageForCommentAsync(long commentId);

    public Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds);
}
