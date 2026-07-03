using Chat.Global.Config;

namespace Chat.Global.Infrastructure;

public static class RedisStartupValidator
{
    public static void Validate(RedisOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            return;

        if (string.IsNullOrWhiteSpace(options.WorkQueueStreamPrefix))
        {
            throw new InvalidOperationException(
                "Redis:WorkQueueStreamPrefix is not configured. Set it in chat-config.yml "
                + "(or Redis__WorkQueueStreamPrefix env).");
        }
    }
}
