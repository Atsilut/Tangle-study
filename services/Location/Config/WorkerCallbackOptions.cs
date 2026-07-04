namespace Location.Config;

public sealed class WorkerCallbackOptions
{
    public const string SectionName = "WorkerCallback";

    public string Secret { get; set; } = string.Empty;
}
