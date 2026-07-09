using System.Text.Json;
using StackExchange.Redis;

namespace Tangle.AspNetCore.Queue;

public sealed class RedisStreamWorkQueue(
    IConnectionMultiplexer connectionMultiplexer,
    IRedisWorkQueueOptions options,
    ILogger<RedisStreamWorkQueue> logger) : IWorkQueue
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _connectionMultiplexer = connectionMultiplexer;
    private readonly IRedisWorkQueueOptions _options = options;
    private readonly ILogger<RedisStreamWorkQueue> _logger = logger;

    public async Task EnqueueAsync<TPayload>(
        string streamKey,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(streamKey)) throw new ArgumentException("Stream key must not be empty.", nameof(streamKey));

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var database = _connectionMultiplexer.GetDatabase();
            var redisStreamKey = BuildStreamKey(streamKey);
            var serializedPayload = JsonSerializer.Serialize(payload, SerializerOptions);
            await database.StreamAddAsync(
                redisStreamKey,
                [
                    new NameValueEntry("type", streamKey),
                    new NameValueEntry("payload", serializedPayload),
                ]);
            WorkQueueMetrics.EnqueueTotal.WithLabels(streamKey).Inc();
            _logger.LogDebug("Enqueued job on stream {Stream}", redisStreamKey);
        }
        catch (Exception ex)
        {
            WorkQueueMetrics.EnqueueFailedTotal.WithLabels(streamKey).Inc();
            _logger.LogWarning(ex, "Failed to enqueue job on stream {StreamKey}", streamKey);
            throw;
        }
    }

    private string BuildStreamKey(string streamKey)
    {
        var prefix = _options.WorkQueueStreamPrefix;
        if (string.IsNullOrWhiteSpace(prefix)) return streamKey;

        return prefix.EndsWith(':') ? $"{prefix}{streamKey}" : $"{prefix}:{streamKey}";
    }
}
