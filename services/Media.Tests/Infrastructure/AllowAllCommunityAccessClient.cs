using Media.Client;

namespace Media.Tests.Infrastructure;

internal sealed class AllowAllCommunityAccessClient : ICommunityAccessClient
{
    public Task EnsureCanViewPostMediaAsync(long postId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task EnsureCanViewCommentMediaAsync(long commentId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
