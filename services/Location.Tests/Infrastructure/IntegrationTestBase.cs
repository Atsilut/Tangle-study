namespace Location.Tests.Infrastructure;

using Location.Client;
using Microsoft.Extensions.DependencyInjection;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected LocationWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }
    protected RedisTestcontainerFixture Redis { get; }
    protected InMemoryMonolithAccessClient MonolithAccess => Factory.MonolithAccess;

    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        RedisTestcontainerFixture redis)
    {
        Redis = redis;
        Factory = new LocationWebApplicationFactory(postgres.ConnectionString, redis.ConnectionString);
        Client = Factory.CreateClient();
        _ = Factory.Services.GetRequiredService<IMonolithAccessClient>();
    }

    public async ValueTask InitializeAsync()
    {
        await Factory.ClearAllLocationDataAsync();
        MonolithAccess.Reset();
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
