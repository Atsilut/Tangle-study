using Api.Domain.Posts.Domain;

namespace Api.Domain.Posts.Repository
{
    public interface IPostRepository
    {
        Task CreatePostAsync(Post post);
        Task<List<Post>?> GetAllPostsAsync();

        Task<Post?> GetPostByIdAsync(long id);
        Task<List<Post>?> GetPostsByUserIdAsync(long userId);
        Task UpdatePostAsync(Post post);
    }
}
