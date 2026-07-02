namespace Media.Tests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected MediaWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }

    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        bool redisEnabled = false,
        string? redisConnectionString = null)
    {
        Factory = new MediaWebApplicationFactory(
            postgres.ConnectionString,
            redisEnabled,
            redisConnectionString);
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
