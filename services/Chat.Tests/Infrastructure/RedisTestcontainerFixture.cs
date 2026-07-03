using Testcontainers.Redis;

namespace Chat.Tests.Infrastructure;

public sealed class RedisTestcontainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _redis;

    public RedisTestcontainerFixture()
    {
        _redis = new RedisBuilder("redis:8.8.0-alpine")
            .Build();
    }

    public string ConnectionString => _redis.GetConnectionString();

    public async ValueTask InitializeAsync() => await _redis.StartAsync();

    public ValueTask DisposeAsync() => _redis.DisposeAsync();
}
