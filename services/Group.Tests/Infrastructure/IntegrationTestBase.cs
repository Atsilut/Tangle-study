using Group.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Group.Tests.Infrastructure;

public abstract class IntegrationTestBase
    : Tangle.TestSupport.Integration.IntegrationTestBase<GroupWebApplicationFactory, Program>
{
    protected InMemoryUserClient InMemoryUser => Factory.InMemoryUser;
    protected FakeCommunityClient FakeCommunity => Factory.FakeCommunityClient;
    protected FakeLocationClient FakeLocation => Factory.FakeLocationClient;

    protected IntegrationTestBase(PostgresTestcontainerFixture postgres)
        : base(
            () => new GroupWebApplicationFactory(postgres.ConnectionString),
            factory => _ = factory.Services.GetRequiredService<IUserClient>())
    {
    }

    protected override async ValueTask ResetStateAsync()
    {
        await Factory.ClearAllGroupDataAsync();
        InMemoryUser.Reset();
        FakeCommunity.Reset();
        FakeLocation.Reset();
        GatewayTestAuthHelpers.ClearAuth(Client);
    }
}
