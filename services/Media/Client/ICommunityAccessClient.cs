namespace Media.Client;

public interface ICommunityAccessClient
{
    public Task EnsureCanViewPostMediaAsync(long postId, CancellationToken cancellationToken = default);

    public Task EnsureCanViewCommentMediaAsync(long commentId, CancellationToken cancellationToken = default);

    public Task<bool> PostExistsAsync(long postId, CancellationToken cancellationToken = default);

    public Task<bool> CommentExistsAsync(long commentId, CancellationToken cancellationToken = default);
}
