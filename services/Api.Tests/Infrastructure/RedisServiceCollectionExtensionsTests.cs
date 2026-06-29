using Api.Global.Infrastructure;
using StackExchange.Redis;

namespace Api.Tests.Infrastructure;

public sealed class RedisServiceCollectionExtensionsTests
{
    [Fact]
    public void ParseRedisConfiguration_SetsAbortOnConnectFailFalse()
    {
        var config = RedisServiceCollectionExtensions.ParseRedisConfiguration("redis:6379");

        Assert.False(config.AbortOnConnectFail);
    }
}
