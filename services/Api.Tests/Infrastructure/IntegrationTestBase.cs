namespace Api.Tests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected ApiWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }

    protected IntegrationTestBase(PostgresTestcontainerFixture postgres)
    {
        Factory = new ApiWebApplicationFactory(postgres.ConnectionString);
        Client = Factory.CreateClient();
    }

    public async ValueTask InitializeAsync() =>
        await Factory.ClearAllEntitiesAsync();

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        return ValueTask.CompletedTask;
    }
}
