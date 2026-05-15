using Api.Domain.Comments.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;

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
    }
}
