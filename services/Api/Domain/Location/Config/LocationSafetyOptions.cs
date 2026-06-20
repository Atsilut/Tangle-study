namespace Api.Domain.Location.Config;

public class LocationSafetyOptions
{
    public const string SectionName = "LocationSafety";

    /// <summary>Minutes without a position update before group members receive a stale alert.</summary>
    public int StalePositionMinutes { get; set; } = 3;

    /// <summary>How often the background monitor scans active sessions.</summary>
    public int MonitorIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Redis TTL for live position keys. When unset (0), defaults to
    /// <see cref="StalePositionMinutes"/> + 2 so stale alerts can fire before keys expire.
    /// </summary>
    public int LivePositionTtlMinutes { get; set; }

    public int ResolveLivePositionTtlMinutes() =>
        LivePositionTtlMinutes > 0 ? LivePositionTtlMinutes : StalePositionMinutes + 2;
}
