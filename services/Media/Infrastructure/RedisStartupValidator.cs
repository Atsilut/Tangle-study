using Media.Global.Config;

namespace Media.Global.Infrastructure;

public static class RedisStartupValidator
{
    public static void Validate(RedisOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            return;

        if (string.IsNullOrWhiteSpace(options.WorkQueueStreamPrefix))
        {
            throw new InvalidOperationException(
                "Redis:WorkQueueStreamPrefix is not configured. Set it in media-config.yml "
                + "(or Redis__WorkQueueStreamPrefix env).");
        }
    }
}
