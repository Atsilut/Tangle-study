using Api.Client;

namespace Api.Client;

public interface IMediaClient
{
    public Task LinkToPostAsync(long postId, long uploaderUserId, IReadOnlyList<long>? mediaAssetIds);

    public Task PatchPostMediaAsync(
        long postId,
        long uploaderUserId,
        IReadOnlyList<long>? addMediaAssetIds,
        IReadOnlyList<long>? removeMediaAssetIds);

    public Task LinkToCommentAsync(long commentId, long uploaderUserId, long? mediaAssetId);

    public Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId);

    public Task<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>> GetMediaByPostIdsAsync(
        IReadOnlyCollection<long> postIds);

    public Task<IReadOnlyList<MediaAssetGetResponseDto>> GetMediaForPostAsync(long postId);

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByCommentIdsAsync(
        IReadOnlyCollection<long> commentIds);

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByChatMessageIdsAsync(
        IReadOnlyCollection<long> chatMessageIds);

    public Task<MediaAssetGetResponseDto?> GetMediaForChatMessageAsync(long chatMessageId);

    public Task DeleteBlobStorageForPostAsync(long postId);

    public Task DeleteBlobStorageForPostsAsync(IReadOnlyCollection<long> postIds);

    public Task DeleteBlobStorageForCommentAsync(long commentId);

    public Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds);

    public Task DeleteBlobStorageForChatMessageAsync(long chatMessageId);

    public Task DetachUploaderFromDeletedUserAsync(long uploaderId);
}
