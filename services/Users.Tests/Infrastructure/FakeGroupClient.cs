using Users.Client;

namespace Users.Tests.Infrastructure;

public sealed class FakeGroupClient : IGroupClient
{
    public List<long> DetachedUserIds { get; } = [];

    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default)
    {
        DetachedUserIds.Add(userId);
        return Task.CompletedTask;
    }
}
