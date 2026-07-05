using Media.Client;

namespace Media.Tests.Infrastructure;

internal sealed class AllowAllUserClient : IUserClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
