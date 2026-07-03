using Location.Config;

namespace Location.Infrastructure;

public static class LocationConfigStartupValidator
{
    public static void Validate(
        LocationSafetyOptions safety,
        LocationClusterOptions cluster,
        PlacesOptions places)
    {
        if (safety.StalePositionMinutes <= 0)
        {
            throw new InvalidOperationException(
                "LocationSafety:StalePositionMinutes is not configured. Set it in location-config.yml.");
        }

        if (safety.LivePositionTtlMinutes <= 0)
        {
            throw new InvalidOperationException(
                "LocationSafety:LivePositionTtlMinutes is not configured. Set it in location-config.yml.");
        }

        if (safety.LivePositionTtlMinutes <= safety.StalePositionMinutes)
        {
            throw new InvalidOperationException(
                "LocationSafety:LivePositionTtlMinutes must be greater than StalePositionMinutes " +
                "so stale alerts can fire before Redis keys expire. Set both in location-config.yml.");
        }

        if (safety.MonitorIntervalSeconds <= 0)
        {
            throw new InvalidOperationException(
                "LocationSafety:MonitorIntervalSeconds is not configured. Set it in location-config.yml.");
        }

        if (safety.SosCooldownSeconds < 0)
        {
            throw new InvalidOperationException(
                "LocationSafety:SosCooldownSeconds is invalid. Set it in location-config.yml.");
        }

        if (cluster.RateLimitPerMinute <= 0)
        {
            throw new InvalidOperationException(
                "LocationCluster:RateLimitPerMinute is not configured. Set it in location-config.yml.");
        }

        if (places.RateLimitPerMinute <= 0)
        {
            throw new InvalidOperationException(
                "Places:RateLimitPerMinute is not configured. Set it in location-config.yml.");
        }
    }
}
