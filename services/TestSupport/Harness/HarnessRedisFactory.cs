namespace Tangle.TestSupport.Harness;

public static class HarnessRedisFactory
{
    public const string RedisConnectionEnv = "TANGLE_HARNESS_REDIS_CONNECTION";

    public static string GetConnectionString() =>
        Environment.GetEnvironmentVariable(RedisConnectionEnv) ?? "redis:6379";
}
