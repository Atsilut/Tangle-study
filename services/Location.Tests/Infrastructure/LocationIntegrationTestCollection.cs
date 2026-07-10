namespace Location.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocationIntegrationTestCollection
    : ICollectionFixture<PostgresTestcontainerFixture>, ICollectionFixture<RedisTestcontainerFixture>
{
    public const string Name = "LocationIntegration";
}
