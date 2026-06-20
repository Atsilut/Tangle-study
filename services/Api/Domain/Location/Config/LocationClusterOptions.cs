namespace Api.Domain.Location.Config;

public sealed class LocationClusterOptions
{
    public const string SectionName = "LocationCluster";

    public int RateLimitPerMinute { get; init; } = 120;
}
