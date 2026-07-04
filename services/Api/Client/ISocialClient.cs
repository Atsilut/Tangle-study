namespace Api.Client;

public interface ISocialClient
{
    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default);
}
