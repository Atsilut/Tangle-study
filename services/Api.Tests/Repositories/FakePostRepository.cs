using Api.Domain.Posts.Domain;
using Api.Domain.Posts.Repository;

namespace Api.Tests.Repositories;

public class FakePostRepository : IPostRepository
{
    private readonly List<Post> _posts = [];
    private long _currentId = 1;

    public Task CreatePostAsync(Post post)
    {
        // Manually set the Id using reflection or a test-only constructor
        var idProperty = typeof(Post).GetProperty("Id");
        if (idProperty != null && idProperty.CanWrite) idProperty.SetValue(post, _currentId++);
        else
            throw new InvalidOperationException("Could not set the Id property on the Post entity via reflection.");
        _posts.Add(post);
        return Task.CompletedTask;
    }

    public Task<List<Post>> GetAllPostsAsync() =>
        Task.FromResult(_posts.Where(p => p.GroupId == null).ToList());

    public Task<List<Post>> GetPostsByGroupBoardAsync(long groupId, long boardId) =>
        Task.FromResult(_posts
            .Where(p => p.GroupId == groupId && p.GroupBoardId == boardId)
            .OrderByDescending(p => p.CreatedAt)
            .ToList());

    public Task<Post?> GetGroupBoardPostAsync(long groupId, long boardId, long postId) =>
        Task.FromResult(_posts.FirstOrDefault(p =>
            p.Id == postId && p.GroupId == groupId && p.GroupBoardId == boardId));

    public Task DeleteAllByGroupAsync(long groupId)
    {
        _posts.RemoveAll(p => p.GroupId == groupId);
        return Task.CompletedTask;
    }

    public Task<List<long>> GetPostIdsByGroupAsync(long groupId) =>
        Task.FromResult(_posts.Where(p => p.GroupId == groupId).Select(p => p.Id).ToList());

    public Task<Post?> GetPostByIdAsync(long id)
    {
        return Task.FromResult(_posts.FirstOrDefault(p => p.Id == id));
    }

    public Task<bool> ExistsPostByIdAsync(long id) =>
        Task.FromResult(_posts.Any(p => p.Id == id));

    public Task<List<Post>> GetPostsByUserIdAsync(long userId) =>
        Task.FromResult(_posts.Where(p => p.UserId == userId).ToList());

    public Task UpdatePostAsync(Post post) => Task.CompletedTask;

    public Task DetachAuthorFromPostsAsync(long userId)
    {
        foreach (var post in _posts.Where(p => p.UserId == userId)) post.DetachAuthor(userId);
        return Task.CompletedTask;
    }

    public Task DeletePostAsync(Post post)
    {
        _posts.Remove(post);
        return Task.CompletedTask;
    }
}
