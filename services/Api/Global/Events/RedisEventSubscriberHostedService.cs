using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Api.Global.Events;

/// <summary>
/// Subscribes to Redis pub/sub channels for logging (and future cross-service handlers).
/// Retries in the background when Redis is not yet reachable so API startup is not blocked.
/// </summary>
public sealed partial class RedisEventSubscriberHostedService(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<RedisEventSubscriberHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private readonly IConnectionMultiplexer _connectionMultiplexer = connectionMultiplexer;
    private readonly ILogger<RedisEventSubscriberHostedService> _logger = logger;
    private volatile bool _subscribed;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SubscribeAsync(stoppingToken);
                _subscribed = true;
                LogRedisEventSubscriberStarted(_logger);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RedisException ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogRedisEventSubscriberConnectFailed(_logger, ex, (int)RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscribed)
        {
            try
            {
                await UnsubscribeAsync();
                LogRedisEventSubscriberStopped(_logger);
            }
            catch (RedisException ex)
            {
                LogRedisEventSubscriberUnsubscribeFailed(_logger, ex);
            }
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        await subscriber.SubscribeAsync(
            RedisChannel.Literal(RedisEventChannels.ChatMessageCreated),
            (_, message) => OnRedisEvent(RedisEventChannels.ChatMessageCreated, message));
        await subscriber.SubscribeAsync(
            RedisChannel.Literal(RedisEventChannels.UserNicknameChanged),
            (_, message) => OnRedisEvent(RedisEventChannels.UserNicknameChanged, message));
    }

    private async Task UnsubscribeAsync()
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        await subscriber.UnsubscribeAsync(RedisChannel.Literal(RedisEventChannels.ChatMessageCreated));
        await subscriber.UnsubscribeAsync(RedisChannel.Literal(RedisEventChannels.UserNicknameChanged));
        _subscribed = false;
    }

    private void OnRedisEvent(string channel, RedisValue message) =>
        LogRedisEventReceived(_logger, channel, message);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Redis event subscriber could not subscribe; retrying in {RetrySeconds}s.")]
    private static partial void LogRedisEventSubscriberConnectFailed(
        ILogger logger,
        Exception exception,
        int retrySeconds);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Redis event subscriber could not unsubscribe during shutdown.")]
    private static partial void LogRedisEventSubscriberUnsubscribeFailed(ILogger logger, Exception exception);

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
