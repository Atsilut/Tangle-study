using Location.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Location.Tests.Infrastructure;

public abstract class IntegrationTestBase
    : Tangle.TestSupport.Integration.IntegrationTestBase<LocationWebApplicationFactory, Program>
{
    protected RedisTestcontainerFixture Redis { get; }
    protected InMemoryUserClient InMemoryUser => Factory.InMemoryUser;
    protected FakeSocialClient FakeSocial => Factory.FakeSocial;
    protected FakeCommunityAccessClient FakeCommunity => Factory.FakeCommunity;
    protected FakeGroupClient FakeGroup => Factory.FakeGroup;

    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        RedisTestcontainerFixture redis)
        : base(
            () => new LocationWebApplicationFactory(postgres.ConnectionString, redis.ConnectionString),
            factory => _ = factory.Services.GetRequiredService<IUserClient>())
    {
        Redis = redis;
    }

    protected override async ValueTask ResetStateAsync()
    {
        await Factory.ClearAllLocationDataAsync();
        InMemoryUser.Reset();
        FakeSocial.Reset();
        FakeCommunity.Reset();
        FakeGroup.Reset();
        GatewayTestAuthHelpers.ClearAuth(Client);
    }
}
