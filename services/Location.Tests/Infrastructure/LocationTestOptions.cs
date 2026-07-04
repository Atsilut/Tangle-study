using Location.Config;

namespace Location.Tests.Infrastructure;

/// <summary>
/// Mirrors values in services/Location/location-config.yml for unit tests that bypass the host.
/// </summary>
internal static class LocationTestOptions
{
    public static LocationSafetyOptions Safety { get; } = new()
    {
        StalePositionMinutes = 3,
        LivePositionTtlMinutes = 5,
        MonitorIntervalSeconds = 60,
        SosCooldownSeconds = 60,
    };

    public static RedisOptions Redis { get; } = new()
    {
        InstanceName = "tangle:",
    };
}
