namespace Api.Global.Config;

public sealed class PlacesOptions
{
    public const string SectionName = "Places";

    public bool Enabled { get; set; }

    /// <summary>Google Cloud API key with Places API (New) and Geocoding API enabled.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Max anonymous requests per IP per minute across Places endpoints. 0 disables limiting.</summary>
    public int RateLimitPerMinute { get; set; } = 30;
}
