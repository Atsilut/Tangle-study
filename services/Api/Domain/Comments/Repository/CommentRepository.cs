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

        public async Task<List<Comment>?> GetCommentsByPostIdAsync(long postId) => await _context.Comments.Where(c => c.PostId == postId).ToListAsync();

        public async Task<List<Comment>?> GetCommentsByUserIdAsync(long userId) => await _context.Comments.Where(c => c.UserId == userId).ToListAsync();

        public async Task UpdateCommentAsync(Comment comment)
        {
            _context.Comments.Update(comment);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCommentAsync(Comment comment)
        {
            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
        }
    }
}
