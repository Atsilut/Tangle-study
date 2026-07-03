namespace Location.Config;

public sealed class PlacesOptions
{
    public const string SectionName = "Places";

    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public int RateLimitPerMinute { get; set; }
}
