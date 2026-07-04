namespace Group.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GroupIntegrationTestCollection : ICollectionFixture<PostgresTestcontainerFixture>
{
    public const string Name = "GroupIntegration";
}
