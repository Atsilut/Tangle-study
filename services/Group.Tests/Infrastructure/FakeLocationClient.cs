using Group.Client;

namespace Group.Tests.Infrastructure;

public sealed class FakeLocationClient : ILocationClient
{
    public List<long> EndedSessionGroupIds { get; } = [];

    public void Reset() => EndedSessionGroupIds.Clear();

    public Task EndSessionsForGroupAsync(long groupId, CancellationToken cancellationToken = default)
    {
        EndedSessionGroupIds.Add(groupId);
        return Task.CompletedTask;
    }
}
