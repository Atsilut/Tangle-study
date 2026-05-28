namespace Api.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class RedisRealtimeIntegrationTestCollection :
    ICollectionFixture<PostgresTestcontainerFixture>,
    ICollectionFixture<RedisTestcontainerFixture>
{
    public const string Name = "RedisRealtimeIntegrationTests";
}
