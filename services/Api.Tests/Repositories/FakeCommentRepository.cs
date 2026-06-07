using Api.Domain.Comments.Domain;
using Api.Domain.Comments.Repository;

namespace Api.Tests.Repositories
{
    public class FakeCommentRepository : ICommentRepository
    {
        private readonly List<Comment> _comments = [];
        private long _currentId = 1;

        public Task CreateCommentAsync(Comment comment)
        {
            // Manually set the Id using reflection or a test-only constructor
            var idProperty = typeof(Comment).GetProperty("Id");
            if (idProperty?.CanWrite is true) idProperty.SetValue(comment, _currentId++);
            else
                throw new InvalidOperationException("Could not set the Id property on the Comment entity via reflection.");
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

        public Task<List<Comment>> GetCommentsByPostIdAsync(long postId) =>
            Task.FromResult(_comments.Where(c => c.PostId == postId).ToList());

        public Task<List<Comment>> GetCommentsByUserIdAsync(long userId) =>
            Task.FromResult(_comments
                .Where(c => c.UserId == userId || c.DeletedUserId == userId)
                .ToList());

        public Task UpdateCommentAsync(Comment comment) => Task.CompletedTask;

        public Task DetachAuthorFromCommentsAsync(long userId)
        {
            foreach (var comment in _comments.Where(c => c.UserId == userId)) comment.DetachAuthor(userId);
            return Task.CompletedTask;
        }

        public Task DetachPostFromCommentsAsync(long postId)
        {
            foreach (var comment in _comments.Where(c => c.PostId == postId)) comment.DetachPost(postId);
            return Task.CompletedTask;
        }

        public Task DeleteAllForPostIdsAsync(IReadOnlyCollection<long> postIds)
        {
            _comments.RemoveAll(c => c.PostId is not null && postIds.Contains(c.PostId.Value));
            return Task.CompletedTask;
        }

        public Task DetachParentFromRepliesAsync(long parentCommentId)
        {
            foreach (var comment in _comments.Where(c => c.ParentId == parentCommentId)) comment.DetachParent(parentCommentId);
            return Task.CompletedTask;
        }

        public Task DeleteCommentAsync(Comment comment)
        {
            _comments.Remove(comment);
            return Task.CompletedTask;
        }
    }
}
