using Api.Domain.Posts.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Posts.Repository
{
    [Repository]
    public class PostRepository(AppDbContext context) : IPostRepository
    {
        private readonly AppDbContext _context = context;

        public Task CreatePostAsync(Post post)
        {
            _context.Posts.Add(post);
            return _context.SaveChangesAsync();
        }

        public Task<List<Post>> GetAllPostsAsync() =>
            _context.Posts.Where(p => p.GroupId == null).ToListAsync();

        public Task<List<Post>> GetPostsByGroupBoardAsync(long groupId, long boardId) =>
            _context.Posts
                .Where(p => p.GroupId == groupId && p.GroupBoardId == boardId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

        public Task<Post?> GetGroupBoardPostAsync(long groupId, long boardId, long postId) =>
            _context.Posts.FirstOrDefaultAsync(p =>
                p.Id == postId && p.GroupId == groupId && p.GroupBoardId == boardId);

        public Task DeleteAllByGroupAsync(long groupId) =>
            _context.Posts.Where(p => p.GroupId == groupId).ExecuteDeleteAsync();

        public Task<List<long>> GetPostIdsByGroupAsync(long groupId) =>
            _context.Posts.Where(p => p.GroupId == groupId).Select(p => p.Id).ToListAsync();

        public Task<Post?> GetPostByIdAsync(long id) => _context.Posts.FindAsync(id).AsTask();

        public Task<List<Post>> GetPostsByIdsAsync(IEnumerable<long> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return Task.FromResult<List<Post>>([]);

            return _context.Posts.Where(p => idList.Contains(p.Id)).ToListAsync();
        }

        public Task<bool> ExistsPostByIdAsync(long id) =>
            _context.Posts.AnyAsync(p => p.Id == id);

        public Task<List<Post>> GetPostsByUserIdAsync(long userId) => _context.Posts
                .Where(post => post.UserId == userId)
                .ToListAsync();

        public Task UpdatePostAsync(Post post) => _context.SaveChangesAsync();

        public async Task DetachAuthorFromPostsAsync(long userId)
        {
            var posts = await _context.Posts.Where(p => p.UserId == userId).ToListAsync();
            foreach (var post in posts)
                post.DetachAuthor(userId);
            await _context.SaveChangesAsync();
        }

        public Task DeletePostAsync(Post post)
        {
            _context.Posts.Remove(post);
            return _context.SaveChangesAsync();
        }
    }
}
