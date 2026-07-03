namespace Media.Config;

public class MetricsOptions
{
    public const string SectionName = "Metrics";

    public bool RequireScrapeSecret { get; set; }

    public string ScrapeSecret { get; set; } = string.Empty;
}
