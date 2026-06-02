namespace Api.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RedisRealtimeIntegrationTestCollection :
    ICollectionFixture<PostgresTestcontainerFixture>,
    ICollectionFixture<RedisTestcontainerFixture>
{
    public const string Name = "RedisRealtimeIntegrationTests";
}
