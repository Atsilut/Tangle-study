namespace Api.Domain.Location.Config;

public class LocationSafetyOptions
{
    public const string SectionName = "LocationSafety";

    /// <summary>Minutes without a position update before group members receive a stale alert.</summary>
    public int StalePositionMinutes { get; set; } = 3;

    /// <summary>How often the background monitor scans active sessions.</summary>
    public int MonitorIntervalSeconds { get; set; } = 60;
}
