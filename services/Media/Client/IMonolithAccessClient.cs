namespace Media.Client;

public interface IMonolithAccessClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default);
}
