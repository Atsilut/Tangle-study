using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Api.Global.Events;

public sealed partial class RedisEventSubscriberHostedService(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<RedisEventSubscriberHostedService> logger) : IHostedService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer = connectionMultiplexer;
    private readonly ILogger<RedisEventSubscriberHostedService> _logger = logger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        await subscriber.SubscribeAsync(
            RedisEventChannels.ChatMessageCreated,
            (_, message) => OnRedisEvent(RedisEventChannels.ChatMessageCreated, message));
        await subscriber.SubscribeAsync(
            RedisEventChannels.UserNicknameChanged,
            (_, message) => OnRedisEvent(RedisEventChannels.UserNicknameChanged, message));
        LogRedisEventSubscriberStarted(_logger);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        await subscriber.UnsubscribeAsync(RedisEventChannels.ChatMessageCreated);
        await subscriber.UnsubscribeAsync(RedisEventChannels.UserNicknameChanged);
        LogRedisEventSubscriberStopped(_logger);
    }

    private void OnRedisEvent(string channel, RedisValue message) =>
        LogRedisEventReceived(_logger, channel, message);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Received event on {Channel}: {Message}")]
    private static partial void LogRedisEventReceived(ILogger logger, string channel, RedisValue message);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Redis event subscriber started.")]
    private static partial void LogRedisEventSubscriberStarted(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Redis event subscriber stopped.")]
    private static partial void LogRedisEventSubscriberStopped(ILogger logger);
}
