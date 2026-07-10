namespace Group.Client;

public interface ILocationClient
{
    public Task EndSessionsForGroupAsync(long groupId, CancellationToken cancellationToken = default);
}
