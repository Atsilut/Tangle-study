using Group.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Group.Tests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected GroupWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }
    protected InMemoryMonolithAccessClient MonolithAccess => Factory.MonolithAccess;
    protected FakeCommunityClient FakeCommunity => Factory.FakeCommunityClient;
    protected FakeLocationClient FakeLocation => Factory.FakeLocationClient;

    protected IntegrationTestBase(PostgresTestcontainerFixture postgres)
    {
        Factory = new GroupWebApplicationFactory(postgres.ConnectionString);
        Client = Factory.CreateClient();
        _ = Factory.Services.GetRequiredService<IMonolithAccessClient>();
    }

    public async ValueTask InitializeAsync()
    {
        await Factory.ClearAllGroupDataAsync();
        MonolithAccess.Reset();
        FakeCommunity.Reset();
        FakeLocation.Reset();
        GroupTestAuthHelpers.ClearAuth(Client);
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
