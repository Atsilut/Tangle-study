namespace Media.Client;

public interface IUserClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default);
}
