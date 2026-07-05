namespace Location.Tests.Infrastructure;

using Location.Client;
using Microsoft.Extensions.DependencyInjection;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected LocationWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }
    protected RedisTestcontainerFixture Redis { get; }
    protected InMemoryUserClient InMemoryUser => Factory.InMemoryUser;
    protected FakeSocialClient FakeSocial => Factory.FakeSocial;
    protected FakeCommunityAccessClient FakeCommunity => Factory.FakeCommunity;
    protected FakeGroupClient FakeGroup => Factory.FakeGroup;

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
        FakeSocial.Reset();
        FakeCommunity.Reset();
        FakeGroup.Reset();
        GatewayTestAuthHelpers.ClearAuth(Client);
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
