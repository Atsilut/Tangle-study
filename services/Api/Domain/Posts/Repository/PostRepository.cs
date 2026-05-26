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

        public async Task<List<Post>> GetAllPostsAsync() =>
            await _context.Posts.Where(p => p.GroupId == null).ToListAsync();

        public async Task<List<Post>> GetPostsByGroupBoardAsync(long groupId, long boardId) =>
            await _context.Posts
                .Where(p => p.GroupId == groupId && p.GroupBoardId == boardId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

        public async Task<Post?> GetGroupBoardPostAsync(long groupId, long boardId, long postId) =>
            await _context.Posts.FirstOrDefaultAsync(p =>
                p.Id == postId && p.GroupId == groupId && p.GroupBoardId == boardId);

        public async Task DeleteAllByGroupAsync(long groupId) =>
            await _context.Posts.Where(p => p.GroupId == groupId).ExecuteDeleteAsync();

        public async Task<Post?> GetPostByIdAsync(long id) => await _context.Posts.FindAsync(id);

        public async Task<bool> ExistsPostByIdAsync(long id) =>
            await _context.Posts.AnyAsync(p => p.Id == id);

        public async Task<List<Post>> GetPostsByUserIdAsync(long userId) => await _context.Posts
                .Where(post => post.UserId == userId)
                .ToListAsync();

        public async Task UpdatePostAsync(Post post) => await _context.SaveChangesAsync();

        public async Task DetachAuthorFromPostsAsync(long userId)
        {
            var posts = await _context.Posts.Where(p => p.UserId == userId).ToListAsync();
            foreach (var post in posts)
                post.DetachAuthor(userId);
            await _context.SaveChangesAsync();
        }

        public async Task DeletePostAsync(Post post)
        {
            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();
        }
    }
}
