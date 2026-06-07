using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Api.Global.Events;

public sealed class RedisEventSubscriberHostedService(
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
            (_, message) => _logger.LogInformation(
                "Received event on {Channel}: {Message}",
                RedisEventChannels.ChatMessageCreated,
                message.ToString()));
        await subscriber.SubscribeAsync(
            RedisEventChannels.UserNicknameChanged,
            (_, message) => _logger.LogInformation(
                "Received event on {Channel}: {Message}",
                RedisEventChannels.UserNicknameChanged,
                message.ToString()));
        _logger.LogInformation("Redis event subscriber started.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        await subscriber.UnsubscribeAsync(RedisEventChannels.ChatMessageCreated);
        await subscriber.UnsubscribeAsync(RedisEventChannels.UserNicknameChanged);
        _logger.LogInformation("Redis event subscriber stopped.");
    }
}
