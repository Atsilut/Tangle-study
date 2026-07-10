using Media.Entities;

namespace Media.Repository;

public interface IMediaAssetRepository
{
    public Task<MediaAsset?> GetMediaAssetByIdAsync(long id);

    public Task<List<MediaAsset>> GetMediaAssetsByIdsAsync(IReadOnlyCollection<long> ids);

    public Task<List<MediaAsset>> GetMediaAssetsByPostIdAsync(long postId);

    public Task<List<MediaAsset>> GetMediaAssetsByPostIdsAsync(IReadOnlyCollection<long> postIds);

    public Task<MediaAsset?> GetMediaAssetByCommentIdAsync(long commentId);

    public Task<IReadOnlyDictionary<long, MediaAsset?>> GetMediaAssetByCommentIdsAsync(IReadOnlyCollection<long> commentIds);

    public Task<MediaAsset?> GetMediaAssetByChatMessageIdAsync(long chatMessageId);

    public Task<IReadOnlyDictionary<long, MediaAsset?>> GetMediaAssetByChatMessageIdsAsync(IReadOnlyCollection<long> chatMessageIds);

    public Task DetachUploaderFromMediaAssetsAsync(long uploaderId);

    public Task CreateMediaAssetAsync(MediaAsset mediaAsset);

    public Task DeleteMediaAssetAsync(MediaAsset mediaAsset);

    public Task SaveChangesAsync();
}
