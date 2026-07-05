namespace Chat.Tests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected ChatWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }
    protected RedisTestcontainerFixture Redis { get; }
    protected InMemoryUserClient InMemoryUser => Factory.InMemoryUser;
    protected FakeMediaClient FakeMediaClient => Factory.FakeMediaClient;

    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        RedisTestcontainerFixture redis)
    {
        Redis = redis;
        Factory = new ChatWebApplicationFactory(postgres.ConnectionString, redis.ConnectionString);
        Client = Factory.CreateClient();
    }

    public async ValueTask InitializeAsync()
    {
        await Factory.ClearAllChatDataAsync();
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
