using Api.Domain.Posts.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Posts.Repository
{
    [Repository]
    public class PostRepository : IPostRepository
    {
        private readonly AppDbContext _context;

        public PostRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreatePostAsync(Post post)
        {
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Post>> GetAllPostsAsync() => await _context.Posts.ToListAsync();

        public async Task<Post?> GetPostByIdAsync(long id) => await _context.Posts.FindAsync(id);

        public async Task<List<Post>> GetPostsByUserIdAsync(long userId) => await _context.Posts
                .Where(post => post.UserId == userId)
                .ToListAsync();

        public async Task UpdatePostAsync(Post post) => await _context.SaveChangesAsync();

        public async Task DeletePostAsync(Post post)
        {
            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();
        }
    }
}
