namespace Media.Tests.Infrastructure;

public abstract class IntegrationTestBase
    : Tangle.TestSupport.Integration.IntegrationTestBase<MediaWebApplicationFactory, Program>
{
    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        RedisTestcontainerFixture redis)
        : base(() => new MediaWebApplicationFactory(postgres.ConnectionString, redis.ConnectionString))
    {
    }

    protected override async ValueTask ResetStateAsync() => await Factory.ClearAllMediaAssetsAsync();
}
