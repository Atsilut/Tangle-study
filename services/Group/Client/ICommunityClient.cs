namespace Group.Client;

public interface ICommunityClient
{
    public Task DeleteAllByGroupAsync(long groupId, CancellationToken cancellationToken = default);
}
