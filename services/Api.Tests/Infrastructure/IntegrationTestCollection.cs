namespace Api.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgresTestcontainerFixture>
{
    public const string Name = "Infrastructure";
}
