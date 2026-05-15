using Api.Domain.Comments.Domain;

namespace Api.Domain.Comments.Repository
{
    public interface ICommentRepository
    {
        public Task CreateCommentAsync(Comment comment);
        public Task<Comment>? GetCommentByIdAsync(long id);
        public Task<List<Comment>?> GetCommentsByPostIdAsync(long postId);
        public Task<List<Comment>?> GetCommentsByUserIdAsync(long userId);
    }
}
