using Community.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Community.Tests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected CommunityWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }
    protected InMemoryMonolithAccessClient MonolithAccess => Factory.MonolithAccess;

    protected IntegrationTestBase(PostgresTestcontainerFixture postgres)
    {
        Factory = new CommunityWebApplicationFactory(postgres.ConnectionString);
        Client = Factory.CreateClient();
        _ = Factory.Services.GetRequiredService<IMonolithAccessClient>();
    }

    public async ValueTask InitializeAsync()
    {
        await Factory.ClearAllCommunityDataAsync();
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
