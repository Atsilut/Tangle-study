using Tangle.AspNetCore.Queue;

namespace Users.Config;

public class RedisOptions : IRedisWorkQueueOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;

    public string InstanceName { get; set; } = "tangle:";

    public string SignalRChannelPrefix { get; set; } = "tangle:signalr:";

    public string WorkQueueStreamPrefix { get; set; } = "tangle:queue:";
}
