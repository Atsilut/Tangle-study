namespace Api.Global.Config;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public bool Enabled { get; set; }

    public string ConnectionString { get; set; } = string.Empty;

    public string InstanceName { get; set; } = "tangle:";

    public string SignalRChannelPrefix { get; set; } = "tangle:signalr:";
}
