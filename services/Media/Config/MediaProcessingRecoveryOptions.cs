namespace Media.Config;

public class MediaProcessingRecoveryOptions
{
    public const string SectionName = "MediaProcessingRecovery";

    /// <summary>How often the sweeper runs. Set 0 to disable.</summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>Re-enqueue media.uploaded when Processing longer than this.</summary>
    public int ReenqueueAfterSeconds { get; set; } = 120;

    /// <summary>Mark Failed with processing_timeout when Processing longer than this.</summary>
    public int FailAfterSeconds { get; set; } = 1800;

    public int BatchSize { get; set; } = 50;
}
