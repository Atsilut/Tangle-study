using Api.Domain.Media.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Media.Repository;

[Repository]
public sealed class MediaAssetRepository(AppDbContext context) : IMediaAssetRepository
{
    private readonly AppDbContext _context = context;

    public Task<MediaAsset?> GetMediaAssetByIdAsync(long id) =>
        _context.MediaAssets.FindAsync(id).AsTask();

    public Task<List<MediaAsset>> GetMediaAssetsByIdsAsync(IReadOnlyCollection<long> ids)
    {
        if (ids.Count == 0) return Task.FromResult<List<MediaAsset>>([]);
        return _context.MediaAssets.Where(m => ids.Contains(m.Id)).ToListAsync();
    }

    public Task<List<MediaAsset>> GetMediaAssetsByPostIdAsync(long postId) =>
        _context.MediaAssets.Where(m => m.PostId == postId).ToListAsync();

    public async Task<MediaAsset?> GetMediaAssetByCommentIdAsync(long commentId)
    {
        var assets = await _context.MediaAssets.Where(m => m.CommentId == commentId).ToListAsync();
        return ToSingleLinkedAsset(assets, $"comment {commentId}");
    }

    public async Task<IReadOnlyDictionary<long, MediaAsset?>> GetMediaAssetByCommentIdsAsync(IReadOnlyCollection<long> commentIds)
    {
        if (commentIds.Count == 0) return new Dictionary<long, MediaAsset?>();

        var assets = await _context.MediaAssets
            .Where(m => m.CommentId != null && commentIds.Contains(m.CommentId.Value))
            .ToListAsync();

        return ToSingleLinkedAssetMap(assets, asset => asset.CommentId!.Value);
    }

    public async Task<MediaAsset?> GetMediaAssetByChatMessageIdAsync(long chatMessageId)
    {
        var assets = await _context.MediaAssets.Where(m => m.ChatMessageId == chatMessageId).ToListAsync();
        return ToSingleLinkedAsset(assets, $"chat message {chatMessageId}");
    }

    public async Task<IReadOnlyDictionary<long, MediaAsset?>> GetMediaAssetByChatMessageIdsAsync(
        IReadOnlyCollection<long> chatMessageIds)
    {
        if (chatMessageIds.Count == 0) return new Dictionary<long, MediaAsset?>();

        var assets = await _context.MediaAssets
            .Where(m => m.ChatMessageId != null && chatMessageIds.Contains(m.ChatMessageId.Value))
            .ToListAsync();

        return ToSingleLinkedAssetMap(assets, asset => asset.ChatMessageId!.Value);
    }

    public async Task DetachUploaderFromMediaAssetsAsync(long uploaderId)
    {
        var assets = await _context.MediaAssets.Where(m => m.UploaderId == uploaderId).ToListAsync();
        foreach (var asset in assets)
            asset.DetachUploader(uploaderId);
        await _context.SaveChangesAsync();
    }

    public Task CreateMediaAssetAsync(MediaAsset mediaAsset)
    {
        _context.MediaAssets.Add(mediaAsset);
        return _context.SaveChangesAsync();
    }

    public Task DeleteMediaAssetAsync(MediaAsset mediaAsset)
    {
        _context.MediaAssets.Remove(mediaAsset);
        return _context.SaveChangesAsync();
    }

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    private static MediaAsset? ToSingleLinkedAsset(IReadOnlyList<MediaAsset> assets, string context)
    {
        if (assets.Count == 0) return null;
        if (assets.Count > 1)
            throw new InvalidOperationException($"Expected at most one media asset for {context}, found {assets.Count}.");

        return assets[0];
    }

    private static Dictionary<long, MediaAsset?> ToSingleLinkedAssetMap(
        IReadOnlyList<MediaAsset> assets,
        Func<MediaAsset, long> keySelector)
    {
        var result = new Dictionary<long, MediaAsset?>();
        foreach (var group in assets.GroupBy(keySelector))
        {
            if (group.Count() > 1)
            {
                throw new InvalidOperationException(
                    $"Expected at most one media asset for linked content {group.Key}, found {group.Count()}.");
            }

            result[group.Key] = group.First();
        }

        return result;
    }
}
