using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Tangle.AspNetCore.Outbox;

public interface IOutboxWriter
{
    public void EnqueueWorkQueue<TPayload>(string streamKey, TPayload payload);

    public void EnqueueEvent<TPayload>(string channel, TPayload payload);
}

public sealed class EfOutboxWriter<TContext>(TContext db) : IOutboxWriter
    where TContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly TContext _db = db;

    public void EnqueueWorkQueue<TPayload>(string streamKey, TPayload payload) =>
        Add(OutboxDestination.WorkQueue, streamKey, payload);

    public void EnqueueEvent<TPayload>(string channel, TPayload payload) =>
        Add(OutboxDestination.Event, channel, payload);

    private void Add<TPayload>(OutboxDestination destination, string target, TPayload payload)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Outbox target must not be empty.", nameof(target));

        _db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Destination = destination,
            Target = target,
            PayloadJson = JsonSerializer.Serialize(payload, SerializerOptions),
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }
}
