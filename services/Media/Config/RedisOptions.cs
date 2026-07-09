using Tangle.AspNetCore.Queue;

namespace Media.Config;

public class RedisOptions : IRedisWorkQueueOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;

    public string WorkQueueStreamPrefix { get; set; } = string.Empty;
}
