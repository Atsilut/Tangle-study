using Api.Domain.Comments.Domain;
using Api.Domain.Comments.Repository;

namespace Api.Tests.Repositories
{
    public class FakeCommentRepository : ICommentRepository
    {
        private readonly List<Comment> _comments = new();
        private long _currentId = 1;

        public Task CreateCommentAsync(Comment comment)
        {
            // Manually set the Id using reflection or a test-only constructor
            var idProperty = typeof(Comment).GetProperty("Id");
            if (idProperty != null && idProperty.CanWrite)
            {
                idProperty.SetValue(comment, _currentId++);
            }
            else throw new InvalidOperationException("Could not set the Id property on the Comment entity via reflection.");
            _comments.Add(comment);
            return Task.CompletedTask;
        }

        public Task<List<Comment>> GetAllCommentsAsync()
        {
            return Task.FromResult(_comments);
        }

        public Task<Comment?> GetCommentByIdAsync(long id)
        {
            return Task.FromResult(_comments.FirstOrDefault(c => c.Id == id));
        }

        public Task<List<Comment>?> GetCommentsByPostIdAsync(long postId)
        {
            var postComments = _comments.Where(c => c.PostId == postId).ToList();
            return Task.FromResult(postComments.Count > 0 ? postComments : null);
        }

        public Task<List<Comment>?> GetCommentsByUserIdAsync(long userId)
        {
            var userComments = _comments.Where(c => c.UserId == userId).ToList();
            return Task.FromResult(userComments.Count > 0 ? userComments : null);
        }

        public Task UpdateCommentAsync(Comment comment)
        {
            var existingComment = _comments.FirstOrDefault(c => c.Id == comment.Id);
            if (existingComment != null)
            {
                _comments.Remove(existingComment);
                _comments.Add(comment);
            }
            return Task.CompletedTask;
        }

        public Task DeleteCommentAsync(Comment comment)
        {
            _comments.Remove(comment);
            return Task.CompletedTask;
        }
    }
}
