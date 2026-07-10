namespace Location.Config;

public class LocationSafetyOptions
{
    public const string SectionName = "LocationSafety";

    /// <summary>Minutes without a position update before group members receive a stale alert.</summary>
    public int StalePositionMinutes { get; set; }

    /// <summary>How often the background monitor scans active sessions.</summary>
    public int MonitorIntervalSeconds { get; set; }

    /// <summary>Redis TTL for live position keys (must exceed <see cref="StalePositionMinutes"/>).</summary>
    public int LivePositionTtlMinutes { get; set; }

    /// <summary>Minimum seconds between SOS alerts from the same live session.</summary>
    public int SosCooldownSeconds { get; set; }
}
