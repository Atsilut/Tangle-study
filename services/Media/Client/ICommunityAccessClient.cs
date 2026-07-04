namespace Media.Client;

public interface ICommunityAccessClient
{
    public Task EnsureCanViewPostMediaAsync(long postId, CancellationToken cancellationToken = default);

    public Task EnsureCanViewCommentMediaAsync(long commentId, CancellationToken cancellationToken = default);
}
