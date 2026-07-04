namespace Media.Config;

public class CommunityClientOptions
{
    public const string SectionName = "CommunityClient";

    public string BaseUrl { get; set; } = string.Empty;

    public string InternalSecret { get; set; } = string.Empty;
}
