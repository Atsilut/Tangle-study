using Microsoft.Extensions.DependencyInjection;
using Social.Client;

namespace Social.Tests.Infrastructure;

public abstract class IntegrationTestBase
    : Tangle.TestSupport.Integration.IntegrationTestBase<SocialWebApplicationFactory, Program>
{
    protected InMemoryUserClient InMemoryUser => Factory.InMemoryUser;

    protected IntegrationTestBase(PostgresTestcontainerFixture postgres)
        : base(
            () => new SocialWebApplicationFactory(postgres.ConnectionString),
            factory => _ = factory.Services.GetRequiredService<IUserClient>())
    {
    }

    protected override async ValueTask ResetStateAsync()
    {
        await Factory.ClearAllSocialDataAsync();
        InMemoryUser.Reset();
        GatewayTestAuthHelpers.ClearAuth(Client);
    }
}
