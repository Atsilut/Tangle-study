using Api.Domain.Posts.Domain;
using Api.Domain.Posts.Repository;

namespace Api.Tests.Fakes;

public class FakePostRepository : IPostRepository
{
    private readonly List<Post> _posts = new();
    private long _currentId = 1;

    public Task CreatePostAsync(Post post)
    {
        var newPost = new Post(post.UserId, post.Title, post.Content);
        // Manually set the Id using reflection or a test-only constructor
        var idProperty = typeof(Post).GetProperty("Id");
        if (idProperty != null && idProperty.CanWrite)
        {
            idProperty.SetValue(newPost, _currentId++);
        }
        else throw new InvalidOperationException("Could not set the Id property on the Post entity via reflection.");
        _posts.Add(newPost);
        return Task.CompletedTask;
    }

    public Task<List<Post>> GetAllPostsAsync()
    {
        return Task.FromResult(_posts);
    }

    public Task<Post?> GetPostByIdAsync(long id)
    {
        return Task.FromResult(_posts.FirstOrDefault(p => p.Id == id));
    }

    public Task<List<Post>?> GetPostsByUserIdAsync(long userId)
    {
        var userPosts = _posts.Where(p => p.UserId == userId).ToList();
        return Task.FromResult(userPosts.Count > 0 ? userPosts : null);
    }

    public Task UpdatePostAsync(Post post)
    {
        var existingPost = _posts.FirstOrDefault(p => p.Id == post.Id);
        if (existingPost != null)
        {
            _posts.Remove(existingPost);
            _posts.Add(post);
        }
        return Task.CompletedTask;
    }

    public Task DeletePostAsync(Post post)
    {
        _posts.Remove(post);
        return Task.CompletedTask;
    }
}
