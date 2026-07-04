namespace Community.Config;

public sealed class SocialClientOptions
{
    public const string SectionName = "SocialClient";

    public string BaseUrl { get; set; } = string.Empty;

    public string InternalSecret { get; set; } = string.Empty;
}
