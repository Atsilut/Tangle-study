namespace Community.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CommunityIntegrationTestCollection : ICollectionFixture<PostgresTestcontainerFixture>
{
    public const string Name = "CommunityIntegration";
}
