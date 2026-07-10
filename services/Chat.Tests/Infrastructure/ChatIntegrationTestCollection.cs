namespace Chat.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ChatIntegrationTestCollection
    : ICollectionFixture<PostgresTestcontainerFixture>, ICollectionFixture<RedisTestcontainerFixture>
{
    public const string Name = "ChatIntegration";
}
