using Media.Global.Config;
using Media.Global.Queue;
using StackExchange.Redis;

namespace Media.Global.Infrastructure;

public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddTangleRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            services.AddSingleton<IWorkQueue, NoOpWorkQueue>();
            return services;
        }

        var redisConfiguration = ParseRedisConfiguration(options.ConnectionString);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConfiguration));
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
