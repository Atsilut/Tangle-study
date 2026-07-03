using Location.Config;
using Location.Queue;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using SignalRRedisOptions = Microsoft.AspNetCore.SignalR.StackExchangeRedis.RedisOptions;

namespace Location.Infrastructure;

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
        var signalRChannelPrefix = options.SignalRChannelPrefix;

        services.AddStackExchangeRedisCache(cache =>
        {
            cache.ConfigurationOptions = redisConfiguration;
            cache.InstanceName = options.InstanceName;
        });

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConfiguration));
        services.AddSingleton<IWorkQueue, RedisStreamWorkQueue>();

        services.AddSingleton<IPostConfigureOptions<SignalRRedisOptions>>(
            sp => new PostConfigureOptions<SignalRRedisOptions>(
                Options.DefaultName,
                signalROptions =>
                {
                    signalROptions.ConnectionFactory ??= async _ =>
                    {
                        await Task.CompletedTask;
                        return sp.GetRequiredService<IConnectionMultiplexer>();
                    };

                    if (!string.IsNullOrWhiteSpace(signalRChannelPrefix))
                    {
                        signalROptions.Configuration ??= redisConfiguration.Clone();
                        signalROptions.Configuration.ChannelPrefix =
                            RedisChannel.Literal(signalRChannelPrefix);
                    }
                }));

        services.AddSignalR().AddStackExchangeRedis();

        return services;
    }

    internal static ConfigurationOptions ParseRedisConfiguration(string connectionString)
    {
        var config = ConfigurationOptions.Parse(connectionString);
        config.AbortOnConnectFail = false;
        return config;
    }
}
