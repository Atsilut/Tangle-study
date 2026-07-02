namespace Media.Client;

public interface IMonolithAccessClient
{
    Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default);

    Task EnsureCanViewPostMediaAsync(long postId, CancellationToken cancellationToken = default);

    Task EnsureCanViewCommentMediaAsync(long commentId, CancellationToken cancellationToken = default);

    Task EnsureCanAccessChatMessageMediaAsync(long chatMessageId, CancellationToken cancellationToken = default);
}
