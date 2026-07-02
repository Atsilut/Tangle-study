namespace Media.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MediaIntegrationTestCollection : ICollectionFixture<PostgresTestcontainerFixture>
{
    public const string Name = "MediaIntegrationTests";
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MediaRedisIntegrationTestCollection :
    ICollectionFixture<PostgresTestcontainerFixture>,
    ICollectionFixture<RedisTestcontainerFixture>
{
    public const string Name = "MediaRedisIntegrationTests";
}
