namespace Users.Client;

public interface ICommunityClient
{
    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default);

    public Task DeleteAllByGroupAsync(long groupId, CancellationToken cancellationToken = default);
}
