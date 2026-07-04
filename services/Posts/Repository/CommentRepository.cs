using Microsoft.EntityFrameworkCore;
using Posts.Db;
using Posts.Entities;
using Posts.Infrastructure;

namespace Posts.Repository;

[Repository]
public class CommentRepository(PostsDbContext context) : ICommentRepository
{
    private readonly PostsDbContext _context = context;

    public Task CreateCommentAsync(Comment comment)
    {
        _context.Comments.Add(comment);
        return _context.SaveChangesAsync();
    }

    public Task<Comment?> GetCommentByIdAsync(long id) => _context.Comments.FindAsync(id).AsTask();

    public Task<List<Comment>> GetCommentsByPostIdAsync(long postId) =>
        _context.Comments.Where(c => c.PostId == postId).ToListAsync();

    public Task<List<Comment>> GetCommentsByUserIdAsync(long userId) =>
        _context.Comments
            .Where(c => c.UserId == userId || c.DeletedUserId == userId)
            .ToListAsync();

    public Task UpdateCommentAsync(Comment comment) => _context.SaveChangesAsync();

    public async Task DetachAuthorFromCommentsAsync(long userId)
    {
        var comments = await _context.Comments.Where(c => c.UserId == userId).ToListAsync();
        foreach (var comment in comments)
            comment.DetachAuthor(userId);
        await _context.SaveChangesAsync();
    }

    public async Task DetachPostFromCommentsAsync(long postId)
    {
        var comments = await _context.Comments.Where(c => c.PostId == postId).ToListAsync();
        foreach (var comment in comments)
            comment.DetachPost(postId);
        await _context.SaveChangesAsync();
    }

    public Task<List<long>> GetCommentIdsByPostIdsAsync(IReadOnlyCollection<long> postIds)
    {
        if (postIds.Count == 0) return Task.FromResult<List<long>>([]);
        return _context.Comments
            .Where(c => c.PostId != null && postIds.Contains(c.PostId.Value))
            .Select(c => c.Id)
            .ToListAsync();
    }

    public Task DeleteAllForPostIdsAsync(IReadOnlyCollection<long> postIds)
    {
        if (postIds.Count == 0) return Task.CompletedTask;
        return _context.Comments
            .Where(c => c.PostId != null && postIds.Contains(c.PostId.Value))
            .ExecuteDeleteAsync();
    }

    public async Task DetachParentFromRepliesAsync(long parentCommentId)
    {
        var replies = await _context.Comments.Where(c => c.ParentId == parentCommentId).ToListAsync();
        foreach (var reply in replies)
            reply.DetachParent(parentCommentId);
        await _context.SaveChangesAsync();
    }

    public Task DeleteCommentAsync(Comment comment)
    {
        _context.Comments.Remove(comment);
        return _context.SaveChangesAsync();
    }
}
