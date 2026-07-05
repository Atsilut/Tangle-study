namespace Users.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UsersIntegrationTestCollection
    : ICollectionFixture<PostgresTestcontainerFixture>, ICollectionFixture<RedisTestcontainerFixture>
{
    public const string Name = "UsersIntegration";
}
