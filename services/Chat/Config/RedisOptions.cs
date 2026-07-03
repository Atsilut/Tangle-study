namespace Chat.Global.Config;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;

    public string WorkQueueStreamPrefix { get; set; } = string.Empty;
}
