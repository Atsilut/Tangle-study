using Chat.Config;
using Chat.Events;
using Chat.Queue;
using StackExchange.Redis;

namespace Chat.Infrastructure;

public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddTangleRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            services.AddSignalR();
            services.AddSingleton<IEventPublisher, NoOpEventPublisher>();
            services.AddSingleton<IWorkQueue, NoOpWorkQueue>();
            return services;
        }

        RedisStartupValidator.Validate(options);

        var redisConfiguration = ParseRedisConfiguration(options.ConnectionString);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConfiguration));
        services.AddSingleton<IEventPublisher, RedisEventPublisher>();
        services.AddSingleton<IWorkQueue, RedisStreamWorkQueue>();

        services.AddSignalR()
            .AddStackExchangeRedis(signalRRedisOptions =>
            {
                signalRRedisOptions.Configuration = redisConfiguration;
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
