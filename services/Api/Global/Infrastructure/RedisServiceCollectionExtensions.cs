using Api.Global.Config;
using Api.Global.Events;
using Api.Global.Queue;
using StackExchange.Redis;

namespace Api.Global.Infrastructure;

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
            services.AddDistributedMemoryCache();
            services.AddSignalR();
            services.AddSingleton<IEventPublisher, NoOpEventPublisher>();
            services.AddSingleton<IWorkQueue, NoOpWorkQueue>();
            return services;
        }

        var redisConfiguration = ParseRedisConfiguration(options.ConnectionString);

        services.AddStackExchangeRedisCache(cache =>
        {
            cache.ConfigurationOptions = redisConfiguration;
            cache.InstanceName = options.InstanceName;
        });

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConfiguration));
        services.AddSingleton<IEventPublisher, RedisEventPublisher>();
        services.AddSingleton<IWorkQueue, RedisStreamWorkQueue>();
        services.AddHostedService<RedisEventSubscriberHostedService>();

        services.AddSignalR()
            .AddStackExchangeRedis(signalRRedisOptions =>
            {
                signalRRedisOptions.Configuration = redisConfiguration;
                if (!string.IsNullOrWhiteSpace(options.SignalRChannelPrefix))
                    signalRRedisOptions.Configuration.ChannelPrefix =
                        RedisChannel.Literal(options.SignalRChannelPrefix);
            });

        return services;
    }

    internal static ConfigurationOptions ParseRedisConfiguration(string connectionString)
    {
        var config = ConfigurationOptions.Parse(connectionString);
        config.AbortOnConnectFail = false;
        return config;
    }
}
