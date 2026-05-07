using Api.Domain.Posts.Domain;

namespace Api.Domain.Posts.Repository
{
    public interface IPostRepository
    {
        Task CreatePostAsync(Post post);
        
    }
}
