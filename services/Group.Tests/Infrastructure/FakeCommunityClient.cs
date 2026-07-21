using Group.Client;

namespace Group.Tests.Infrastructure;

public sealed class FakeCommunityClient : ICommunityClient
{
    public List<long> DeletedGroupIds { get; } = [];
    public Exception? DeleteAllFailure { get; set; }

    public void Reset()
    {
        DeletedGroupIds.Clear();
        DeleteAllFailure = null;
    }

    public Task DeleteAllByGroupAsync(long groupId, CancellationToken cancellationToken = default)
    {
        if (DeleteAllFailure is not null) throw DeleteAllFailure;
        DeletedGroupIds.Add(groupId);
        return Task.CompletedTask;
    }
}
