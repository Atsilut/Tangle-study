namespace Media.Config;

public class MediaReconciliationOptions
{
    public const string SectionName = "MediaReconciliation";

    /// <summary>How often the orphan-link reconciler runs. Set 0 to disable.</summary>
    public int IntervalSeconds { get; set; } = 300;

    /// <summary>Max linked assets inspected per tick.</summary>
    public int BatchSize { get; set; } = 100;
}
