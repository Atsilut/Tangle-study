namespace Media.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MediaIntegrationTestCollection :
    ICollectionFixture<PostgresTestcontainerFixture>,
    ICollectionFixture<RedisTestcontainerFixture>
{
    public const string Name = "MediaIntegrationTests";
}
