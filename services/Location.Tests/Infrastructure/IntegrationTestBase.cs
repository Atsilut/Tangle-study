namespace Location.Tests.Infrastructure;

using Location.Client;
using Microsoft.Extensions.DependencyInjection;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected LocationWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }
    protected RedisTestcontainerFixture Redis { get; }
    protected InMemoryUserClient InMemoryUser => Factory.InMemoryUser;

    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        RedisTestcontainerFixture redis)
    {
        Redis = redis;
        Factory = new LocationWebApplicationFactory(postgres.ConnectionString, redis.ConnectionString);
        Client = Factory.CreateClient();
        _ = Factory.Services.GetRequiredService<IUserClient>();
    }

    public async ValueTask InitializeAsync()
    {
        await Factory.ClearAllLocationDataAsync();
        InMemoryUser.Reset();
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
