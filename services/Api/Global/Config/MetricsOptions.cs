namespace Api.Global.Config;

public class MetricsOptions
{
    public const string SectionName = "Metrics";

    public bool RequireAuth { get; set; }

    public string Secret { get; set; } = string.Empty;
}
