using Api.Domain.Media.Dto;

namespace Api.Client;

public interface IMediaClient
{
    Task LinkToPostAsync(long postId, long uploaderUserId, IReadOnlyList<long>? mediaAssetIds);

    Task PatchPostMediaAsync(
        long postId,
        long uploaderUserId,
        IReadOnlyList<long>? addMediaAssetIds,
        IReadOnlyList<long>? removeMediaAssetIds);

    Task LinkToCommentAsync(long commentId, long uploaderUserId, long? mediaAssetId);

    Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId);

    Task<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>> GetMediaByPostIdsAsync(
        IReadOnlyCollection<long> postIds);

    Task<IReadOnlyList<MediaAssetGetResponseDto>> GetMediaForPostAsync(long postId);

    Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByCommentIdsAsync(
        IReadOnlyCollection<long> commentIds);

    Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByChatMessageIdsAsync(
        IReadOnlyCollection<long> chatMessageIds);

    Task<MediaAssetGetResponseDto?> GetMediaForChatMessageAsync(long chatMessageId);

    Task DeleteBlobStorageForPostAsync(long postId);

    Task DeleteBlobStorageForPostsAsync(IReadOnlyCollection<long> postIds);

    Task DeleteBlobStorageForCommentAsync(long commentId);

    Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds);

    Task DeleteBlobStorageForChatMessageAsync(long chatMessageId);

    Task DetachUploaderFromDeletedUserAsync(long uploaderId);
}
