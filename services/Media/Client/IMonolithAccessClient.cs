namespace Media.Client;

public interface IMonolithAccessClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default);

    public Task EnsureCanViewPostMediaAsync(long postId, CancellationToken cancellationToken = default);

    public Task EnsureCanViewCommentMediaAsync(long commentId, CancellationToken cancellationToken = default);
}
