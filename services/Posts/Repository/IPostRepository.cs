using Posts.Entities;

namespace Posts.Repository;

public interface IPostRepository
{
    public Task CreatePostAsync(Post post);
    public Task<List<Post>> GetAllPostsAsync();
    public Task<List<Post>> GetPostsByGroupBoardAsync(long groupId, long boardId);
    public Task<Post?> GetGroupBoardPostAsync(long groupId, long boardId, long postId);
    public Task DeleteAllByGroupAsync(long groupId);
    public Task<List<long>> GetPostIdsByGroupAsync(long groupId);
    public Task<Post?> GetPostByIdAsync(long id);
    public Task<List<Post>> GetPostsByIdsAsync(IEnumerable<long> ids);
    public Task<bool> ExistsPostByIdAsync(long id);
    public Task<List<Post>> GetPostsByUserIdAsync(long userId);
    public Task UpdatePostAsync(Post post);
    public Task DetachAuthorFromPostsAsync(long userId);
    public Task DeletePostAsync(Post post);
}
