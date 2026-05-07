using Api.Domain.Posts.Domain;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Posts.Repository
{
    [Repository]
    public class PostRepository : IPostRepository
    {
        private readonly Global.Db.AppDbContext _context;

        public PostRepository(Global.Db.AppDbContext context)
        {
            _context = context;
        }

        public async Task CreatePostAsync(Post post)
        {
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();
        }
    }
}
