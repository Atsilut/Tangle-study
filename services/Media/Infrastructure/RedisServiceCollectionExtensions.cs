using Media.Config;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Tangle.AspNetCore.Queue;

namespace Media.Infrastructure;

public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddTangleRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        RedisStartupValidator.Validate(options);

        var redisConfiguration = ParseRedisConfiguration(options.ConnectionString);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConfiguration));
        services.AddSingleton<IRedisWorkQueueOptions>(sp => sp.GetRequiredService<IOptions<RedisOptions>>().Value);
        services.AddSingleton<IWorkQueue, RedisStreamWorkQueue>();

        return services;
    }

    internal static ConfigurationOptions ParseRedisConfiguration(string connectionString)
    {
        var config = ConfigurationOptions.Parse(connectionString);
        config.AbortOnConnectFail = false;
        return config;
    }
}
