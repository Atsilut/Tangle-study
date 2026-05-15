using Api.Domain.Comments.Domain;

namespace Api.Domain.Comments.Repository
{
    public interface ICommentRepository
    {
        public Task CreateCommentAsync(Comment comment);
    }
}
