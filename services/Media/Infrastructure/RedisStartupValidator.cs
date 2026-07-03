using Media.Config;

namespace Media.Infrastructure;

public static class RedisStartupValidator
{
    public static void Validate(RedisOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                "Redis:ConnectionString is not configured. Start Redis (docker compose up redis) "
                + "or set Redis__ConnectionString.");
        }

        if (string.IsNullOrWhiteSpace(options.WorkQueueStreamPrefix))
        {
            throw new InvalidOperationException(
                "Redis:WorkQueueStreamPrefix is not configured. Set it in media-config.yml "
                + "(or Redis__WorkQueueStreamPrefix env).");
        }
    }
}
