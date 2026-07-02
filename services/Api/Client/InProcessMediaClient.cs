using Api.Domain.Media.Dto;
using Api.Domain.Media.Service;

namespace Api.Client;

public sealed class InProcessMediaClient(MediaService mediaService) : IMediaClient
{
    private readonly MediaService _mediaService = mediaService;

    public Task LinkToPostAsync(long postId, long uploaderUserId, IReadOnlyList<long>? mediaAssetIds) =>
        _mediaService.LinkToPostAsync(postId, uploaderUserId, mediaAssetIds);

    public Task PatchPostMediaAsync(
        long postId,
        long uploaderUserId,
        IReadOnlyList<long>? addMediaAssetIds,
        IReadOnlyList<long>? removeMediaAssetIds) =>
        _mediaService.PatchPostMediaAsync(postId, uploaderUserId, addMediaAssetIds, removeMediaAssetIds);

    public Task LinkToCommentAsync(long commentId, long uploaderUserId, long? mediaAssetId) =>
        _mediaService.LinkToCommentAsync(commentId, uploaderUserId, mediaAssetId);

    public Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId) =>
        _mediaService.LinkToChatMessageAsync(chatMessageId, senderUserId, mediaAssetId);

    public Task<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>> GetMediaByPostIdsAsync(
        IReadOnlyCollection<long> postIds) =>
        _mediaService.GetMediaByPostIdsAsync(postIds);

    public Task<IReadOnlyList<MediaAssetGetResponseDto>> GetMediaForPostAsync(long postId) =>
        _mediaService.GetMediaForPostAsync(postId);

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByCommentIdsAsync(
        IReadOnlyCollection<long> commentIds) =>
        _mediaService.GetMediaByCommentIdsAsync(commentIds);

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByChatMessageIdsAsync(
        IReadOnlyCollection<long> chatMessageIds) =>
        _mediaService.GetMediaByChatMessageIdsAsync(chatMessageIds);

    public Task<MediaAssetGetResponseDto?> GetMediaForChatMessageAsync(long chatMessageId) =>
        _mediaService.GetMediaForChatMessageAsync(chatMessageId);

    public Task DeleteBlobStorageForPostAsync(long postId) =>
        _mediaService.DeleteBlobStorageForPostAsync(postId);

    public Task DeleteBlobStorageForPostsAsync(IReadOnlyCollection<long> postIds) =>
        _mediaService.DeleteBlobStorageForPostsAsync(postIds);

    public Task DeleteBlobStorageForCommentAsync(long commentId) =>
        _mediaService.DeleteBlobStorageForCommentAsync(commentId);

    public Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds) =>
        _mediaService.DeleteBlobStorageForCommentsAsync(commentIds);

    public Task DeleteBlobStorageForChatMessageAsync(long chatMessageId) =>
        _mediaService.DeleteBlobStorageForChatMessageAsync(chatMessageId);

    public Task DetachUploaderFromDeletedUserAsync(long uploaderId) =>
        _mediaService.DetachUploaderFromDeletedUserAsync(uploaderId);
}
