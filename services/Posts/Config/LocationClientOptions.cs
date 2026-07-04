namespace Posts.Config;

public sealed class LocationClientOptions
{
    public const string SectionName = "LocationClient";

    public string BaseUrl { get; set; } = string.Empty;

    public string InternalSecret { get; set; } = string.Empty;
}
