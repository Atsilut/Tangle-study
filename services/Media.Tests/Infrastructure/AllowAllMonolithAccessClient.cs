using Media.Client;

namespace Media.Tests.Infrastructure;

internal sealed class AllowAllMonolithAccessClient : IMonolithAccessClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task EnsureCanViewPostMediaAsync(long postId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task EnsureCanViewCommentMediaAsync(long commentId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task EnsureCanAccessChatMessageMediaAsync(long chatMessageId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
