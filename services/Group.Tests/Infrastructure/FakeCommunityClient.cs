using Group.Client;

namespace Group.Tests.Infrastructure;

public sealed class FakeCommunityClient : ICommunityClient
{
    public List<long> DeletedGroupIds { get; } = [];

    public void Reset() => DeletedGroupIds.Clear();

    public Task DeleteAllByGroupAsync(long groupId, CancellationToken cancellationToken = default)
    {
        DeletedGroupIds.Add(groupId);
        return Task.CompletedTask;
    }
}
