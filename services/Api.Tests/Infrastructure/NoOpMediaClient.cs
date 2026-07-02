using Api.Client;
using Api.Domain.Media.Dto;

namespace Api.Tests.Infrastructure;

internal sealed class NoOpMediaClient : IMediaClient
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

    public Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId) =>
        Task.CompletedTask;

    public Task<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>> GetMediaByPostIdsAsync(
        IReadOnlyCollection<long> postIds) =>
        Task.FromResult<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>>(
            new Dictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>());

    public Task<IReadOnlyList<MediaAssetGetResponseDto>> GetMediaForPostAsync(long postId) =>
        Task.FromResult<IReadOnlyList<MediaAssetGetResponseDto>>([]);

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByCommentIdsAsync(
        IReadOnlyCollection<long> commentIds) =>
        Task.FromResult<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>>(
            new Dictionary<long, MediaAssetGetResponseDto?>());

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByChatMessageIdsAsync(
        IReadOnlyCollection<long> chatMessageIds) =>
        Task.FromResult<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>>(
            new Dictionary<long, MediaAssetGetResponseDto?>());

    public Task<MediaAssetGetResponseDto?> GetMediaForChatMessageAsync(long chatMessageId) =>
        Task.FromResult<MediaAssetGetResponseDto?>(null);

    public Task DeleteBlobStorageForPostAsync(long postId) => Task.CompletedTask;

    public Task DeleteBlobStorageForPostsAsync(IReadOnlyCollection<long> postIds) => Task.CompletedTask;

    public Task DeleteBlobStorageForCommentAsync(long commentId) => Task.CompletedTask;

    public Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds) => Task.CompletedTask;

    public Task DeleteBlobStorageForChatMessageAsync(long chatMessageId) => Task.CompletedTask;

    public Task DetachUploaderFromDeletedUserAsync(long uploaderId) => Task.CompletedTask;
}
