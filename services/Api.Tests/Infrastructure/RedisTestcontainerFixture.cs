using Testcontainers.Redis;

namespace Api.Tests.Infrastructure;

public sealed class RedisTestcontainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _redis;

    public RedisTestcontainerFixture()
    {
        _redis = new RedisBuilder()
            .WithImage("redis:8.8-alpine")
            .Build();
    }

    public string ConnectionString => $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}";

    public async ValueTask InitializeAsync() => await _redis.StartAsync();

    public async ValueTask DisposeAsync() => await _redis.DisposeAsync();
}
