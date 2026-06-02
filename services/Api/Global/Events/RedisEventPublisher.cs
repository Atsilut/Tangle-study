using System.Text.Json;
using StackExchange.Redis;

namespace Api.Global.Events;

public sealed class RedisEventPublisher : IEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisEventPublisher> _logger;

    public RedisEventPublisher(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisEventPublisher> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;
    }

    public async Task PublishAsync<TPayload>(string channel, TPayload payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channel))
            throw new ArgumentException("Channel must not be empty.", nameof(channel));

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var subscriber = _connectionMultiplexer.GetSubscriber();
            var serializedPayload = JsonSerializer.Serialize(payload, SerializerOptions);
            await subscriber.PublishAsync(channel, serializedPayload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish Redis event on channel {Channel}", channel);
        }
    }
}
