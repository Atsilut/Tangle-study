using Users.Config;

namespace Users.Infrastructure;

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
    }
}
