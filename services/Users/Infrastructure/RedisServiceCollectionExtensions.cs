using Users.Config;
using Users.Events;
using StackExchange.Redis;

namespace Users.Infrastructure;

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

        services.AddStackExchangeRedisCache(cache =>
        {
            cache.ConfigurationOptions = redisConfiguration;
            cache.InstanceName = options.InstanceName;
        });

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConfiguration));
        services.AddSingleton<IEventPublisher, RedisEventPublisher>();
        services.AddHostedService<RedisEventSubscriberHostedService>();

        return services;
    }

    internal static ConfigurationOptions ParseRedisConfiguration(string connectionString)
    {
        var config = ConfigurationOptions.Parse(connectionString);
        config.AbortOnConnectFail = false;
        return config;
    }
}
