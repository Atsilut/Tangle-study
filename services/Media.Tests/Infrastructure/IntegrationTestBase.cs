namespace Media.Tests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected MediaWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }

    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        RedisTestcontainerFixture redis)
    {
        Factory = new MediaWebApplicationFactory(
            postgres.ConnectionString,
            redis.ConnectionString);
        Client = Factory.CreateClient();
    }

    public async ValueTask InitializeAsync() => await Factory.ClearAllMediaAssetsAsync();

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
