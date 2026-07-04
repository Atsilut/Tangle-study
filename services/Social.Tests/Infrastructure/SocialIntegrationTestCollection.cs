namespace Social.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SocialIntegrationTestCollection : ICollectionFixture<PostgresTestcontainerFixture>
{
    public const string Name = "SocialIntegration";
}
