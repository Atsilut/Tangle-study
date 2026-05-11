using Api.Domain.Posts.Domain;

namespace Api.Domain.Posts.Repository
{
    public interface IPostRepository
    {
        public Task CreatePostAsync(Post post);
        public Task<List<Post>?> GetAllPostsAsync();

        public Task<Post?> GetPostByIdAsync(long id);
        public Task<List<Post>?> GetPostsByUserIdAsync(long userId);
        public Task UpdatePostAsync(Post post);
        public Task DeletePostAsync(Post post);
    }
}
