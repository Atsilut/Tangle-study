namespace Chat.Tests.Infrastructure;

public abstract class IntegrationTestBase
    : Tangle.TestSupport.Integration.IntegrationTestBase<ChatWebApplicationFactory, Program>
{
    protected RedisTestcontainerFixture Redis { get; }
    protected InMemoryUserClient InMemoryUser => Factory.InMemoryUser;
    protected FakeMediaClient FakeMediaClient => Factory.FakeMediaClient;

    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        RedisTestcontainerFixture redis)
        : base(() => new ChatWebApplicationFactory(postgres.ConnectionString, redis.ConnectionString))
    {
        Redis = redis;
    }

    protected override async ValueTask ResetStateAsync()
    {
        await Factory.ClearAllChatDataAsync();
        InMemoryUser.Reset();
        GatewayTestAuthHelpers.ClearAuth(Client);
    }
}
