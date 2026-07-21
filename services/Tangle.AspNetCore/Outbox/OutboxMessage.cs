namespace Tangle.AspNetCore.Outbox;

public enum OutboxDestination
{
    WorkQueue = 0,
    Event = 1,
}

public sealed class OutboxMessage
{
    public long Id { get; set; }

    public OutboxDestination Destination { get; set; }

    /// <summary>Stream key (work queue) or pub/sub channel (event).</summary>
    public string Target { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset? DeadLetteredAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
