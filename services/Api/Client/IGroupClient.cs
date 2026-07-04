namespace Api.Client;

public interface IGroupClient
{
    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default);
}
