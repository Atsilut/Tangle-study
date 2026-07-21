namespace Tangle.AspNetCore.Outbox;

public class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>How often the dispatcher polls for pending rows.</summary>
    public int PollIntervalMilliseconds { get; set; } = 1000;

    /// <summary>Max rows processed per tick.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Attempts before dead-lettering a row.</summary>
    public int MaxAttempts { get; set; } = 10;
}
