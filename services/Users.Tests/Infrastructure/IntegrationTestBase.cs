namespace Users.Tests.Infrastructure;

public abstract class IntegrationTestBase
    : Tangle.TestSupport.Integration.IntegrationTestBase<UsersWebApplicationFactory, UsersProgram>
{
    protected RedisTestcontainerFixture Redis { get; }

    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        RedisTestcontainerFixture redis)
        : base(() => new UsersWebApplicationFactory(postgres.ConnectionString, redis.ConnectionString))
    {
        Redis = redis;
    }

    protected override async ValueTask ResetStateAsync() => await Factory.ClearAllUsersAsync();
}
