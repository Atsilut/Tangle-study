using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Tangle.AspNetCore.Outbox;

public interface IOutboxWriter
{
    public void EnqueueWorkQueue<TPayload>(string streamKey, TPayload payload, long? entityId = null);

    public void EnqueueEvent<TPayload>(string channel, TPayload payload, long? entityId = null);
}

public sealed class EfOutboxWriter<TContext>(TContext db) : IOutboxWriter
    where TContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly TContext _db = db;

    public void EnqueueWorkQueue<TPayload>(string streamKey, TPayload payload, long? entityId = null) =>
        Add(OutboxDestination.WorkQueue, streamKey, payload, entityId);

    public void EnqueueEvent<TPayload>(string channel, TPayload payload, long? entityId = null) =>
        Add(OutboxDestination.Event, channel, payload, entityId);

    private void Add<TPayload>(OutboxDestination destination, string target, TPayload payload, long? entityId)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Outbox target must not be empty.", nameof(target));

        _db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Destination = destination,
            Target = target,
            EntityId = entityId,
            PayloadJson = JsonSerializer.Serialize(payload, SerializerOptions),
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }
}
