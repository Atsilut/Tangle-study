using Location.Config;

namespace Location.Infrastructure;

public static class RedisStartupValidator
{
    public static void Validate(RedisOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Redis:ConnectionString is not configured. Start Redis (docker compose up redis) "
                + "and set the connection string.");
        }

        if (string.IsNullOrWhiteSpace(options.InstanceName))
        {
            throw new InvalidOperationException(
                "Redis:InstanceName is not configured. Set it in location-config.yml "
                + "(or Redis__InstanceName env).");
        }

        if (string.IsNullOrWhiteSpace(options.WorkQueueStreamPrefix))
        {
            throw new InvalidOperationException(
                "Redis:WorkQueueStreamPrefix is not configured. Set it in location-config.yml "
                + "(or Redis__WorkQueueStreamPrefix env).");
        }

        if (string.IsNullOrWhiteSpace(options.SignalRChannelPrefix))
        {
            throw new InvalidOperationException(
                "Redis:SignalRChannelPrefix is not configured. Set it in location-config.yml "
                + "(or Redis__SignalRChannelPrefix env).");
        }
    }
}
