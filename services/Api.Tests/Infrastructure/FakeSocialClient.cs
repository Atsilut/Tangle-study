using Api.Client;

namespace Api.Tests.Infrastructure;

public sealed class FakeSocialClient : ISocialClient
{
    public List<long> DetachedUserIds { get; } = [];

    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default)
    {
        DetachedUserIds.Add(userId);
        return Task.CompletedTask;
    }
}
