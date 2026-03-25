namespace Api.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgresTestcontainerFixture>
{
    public const string Name = "Infrastructure";
}
