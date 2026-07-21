using Media.Client;

namespace Media.Tests.Infrastructure;

/// <summary>
/// Community-access fake with configurable post/comment existence for reconciliation tests.
/// </summary>
internal sealed class ConfigurableCommunityAccessClient : ICommunityAccessClient
{
    private readonly HashSet<long> _existingPostIds = [];
    private readonly HashSet<long> _existingCommentIds = [];

    public void Reset()
    {
        _existingPostIds.Clear();
        _existingCommentIds.Clear();
    }

    public void SetPostExists(long postId, bool exists = true)
    {
        if (exists) _existingPostIds.Add(postId);
        else _existingPostIds.Remove(postId);
    }

    public void SetCommentExists(long commentId, bool exists = true)
    {
        if (exists) _existingCommentIds.Add(commentId);
        else _existingCommentIds.Remove(commentId);
    }

    public Task EnsureCanViewPostMediaAsync(long postId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task EnsureCanViewCommentMediaAsync(long commentId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> PostExistsAsync(long postId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_existingPostIds.Contains(postId));

    public Task<bool> CommentExistsAsync(long commentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_existingCommentIds.Contains(commentId));
}
