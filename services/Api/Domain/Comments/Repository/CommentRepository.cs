using Api.Domain.Comments.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Comments.Repository
{
    [Repository]
    public class CommentRepository : ICommentRepository
    {
        private readonly AppDbContext _context;

        public CommentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateCommentAsync(Comment comment)
        {
            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();
        }

        public async Task<Comment?> GetCommentByIdAsync(long id) => await _context.Comments.FindAsync(id);

        public async Task<List<Comment>> GetCommentsByPostIdAsync(long postId) =>
            await _context.Comments.Where(c => c.PostId == postId).ToListAsync();

        public async Task<List<Comment>> GetCommentsByUserIdAsync(long userId) =>
            await _context.Comments
                .Where(c => c.UserId == userId || c.DeletedUserId == userId)
                .ToListAsync();

        public async Task UpdateCommentAsync(Comment comment) => await _context.SaveChangesAsync();

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

        public async Task DeleteAllForPostIdsAsync(IReadOnlyCollection<long> postIds)
        {
            if (postIds.Count == 0) return;
            await _context.Comments
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

        public async Task DeleteCommentAsync(Comment comment)
        {
            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
        }
    }
}
