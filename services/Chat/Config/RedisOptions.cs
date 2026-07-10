using Tangle.AspNetCore.Queue;

namespace Chat.Config;

public class RedisOptions : IRedisWorkQueueOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;

    public string WorkQueueStreamPrefix { get; set; } = string.Empty;

    public string SignalRChannelPrefix { get; set; } = string.Empty;
}
