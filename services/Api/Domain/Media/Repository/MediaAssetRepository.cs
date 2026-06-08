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

    public Task<List<MediaAsset>> GetMediaAssetsByPostIdAsync(long postId) =>
        _context.MediaAssets.Where(m => m.PostId == postId).ToListAsync();

    public Task<List<MediaAsset>> GetMediaAssetsByCommentIdAsync(long commentId) =>
        _context.MediaAssets.Where(m => m.CommentId == commentId).ToListAsync();

    public Task<List<MediaAsset>> GetMediaAssetsByCommentIdsAsync(IReadOnlyCollection<long> commentIds)
    {
        if (commentIds.Count == 0) return Task.FromResult<List<MediaAsset>>([]);
        return _context.MediaAssets.Where(m => m.CommentId != null && commentIds.Contains(m.CommentId.Value)).ToListAsync();
    }

    public Task<List<MediaAsset>> GetMediaAssetsByChatMessageIdAsync(long chatMessageId) =>
        _context.MediaAssets.Where(m => m.ChatMessageId == chatMessageId).ToListAsync();

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
}
