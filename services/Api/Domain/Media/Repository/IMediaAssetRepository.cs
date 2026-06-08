using Api.Domain.Media.Domain;

namespace Api.Domain.Media.Repository;

public interface IMediaAssetRepository
{
    public Task<MediaAsset?> GetMediaAssetByIdAsync(long id);

    public Task<List<MediaAsset>> GetMediaAssetsByPostIdAsync(long postId);

    public Task<List<MediaAsset>> GetMediaAssetsByCommentIdAsync(long commentId);

    public Task<List<MediaAsset>> GetMediaAssetsByCommentIdsAsync(IReadOnlyCollection<long> commentIds);

    public Task<List<MediaAsset>> GetMediaAssetsByChatMessageIdAsync(long chatMessageId);

    public Task DetachUploaderFromMediaAssetsAsync(long uploaderId);

    public Task CreateMediaAssetAsync(MediaAsset mediaAsset);

    public Task DeleteMediaAssetAsync(MediaAsset mediaAsset);

    public Task SaveChangesAsync();
}
