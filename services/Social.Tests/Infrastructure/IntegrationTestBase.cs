using Microsoft.Extensions.DependencyInjection;
using Social.Client;

namespace Social.Tests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected SocialWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }
    protected InMemoryUserClient InMemoryUser => Factory.InMemoryUser;

    protected IntegrationTestBase(PostgresTestcontainerFixture postgres)
    {
        Factory = new SocialWebApplicationFactory(postgres.ConnectionString);
        Client = Factory.CreateClient();
        _ = Factory.Services.GetRequiredService<IUserClient>();
    }

    public async ValueTask InitializeAsync()
    {
        await Factory.ClearAllSocialDataAsync();
        InMemoryUser.Reset();
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
