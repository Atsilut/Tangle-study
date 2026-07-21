using System.Text.Json;
using Chat.Events;
using Tangle.AspNetCore.Outbox;

namespace Chat.Infrastructure;

/// <summary>
/// Publishes outbox event rows via the Chat Redis pub/sub publisher.
/// </summary>
public sealed class ChatOutboxEventPublisher(IEventPublisher eventPublisher) : IOutboxEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IEventPublisher _eventPublisher = eventPublisher;

    public Task PublishAsync(string channel, string payloadJson, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        return _eventPublisher.PublishAsync(channel, doc.RootElement, cancellationToken);
    }
}
