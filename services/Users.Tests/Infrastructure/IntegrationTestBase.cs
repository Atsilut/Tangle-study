namespace Users.Tests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected UsersWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }
    protected RedisTestcontainerFixture Redis { get; }

    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        RedisTestcontainerFixture redis)
    {
        Redis = redis;
        Factory = new UsersWebApplicationFactory(postgres.ConnectionString, redis.ConnectionString);
        Client = Factory.CreateClient();
    }

    public async ValueTask InitializeAsync() => await Factory.ClearAllUsersAsync();

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
