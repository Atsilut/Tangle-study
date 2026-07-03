namespace Location.Config;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;

    public string InstanceName { get; set; } = string.Empty;

    public string WorkQueueStreamPrefix { get; set; } = string.Empty;

    public string SignalRChannelPrefix { get; set; } = string.Empty;
}
