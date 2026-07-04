using Media.Client;

namespace Media.Tests.Infrastructure;

internal sealed class AllowAllMonolithAccessClient : IMonolithAccessClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
