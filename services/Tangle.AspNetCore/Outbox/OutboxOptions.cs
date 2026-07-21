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

    /// <summary>How long to keep processed rows before pruning. <c>0</c> keeps them forever.</summary>
    public int RetentionHours { get; set; } = 72;

    /// <summary>How often the dispatcher prunes processed rows past the retention window.</summary>
    public int PruneIntervalMinutes { get; set; } = 30;
}
