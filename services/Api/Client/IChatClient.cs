namespace Api.Client;

public interface IChatClient
{
    Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default);
}
