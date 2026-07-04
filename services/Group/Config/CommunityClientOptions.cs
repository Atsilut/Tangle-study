namespace Group.Config;

public sealed class CommunityClientOptions
{
    public const string SectionName = "CommunityClient";

    public string BaseUrl { get; set; } = string.Empty;

    public string InternalSecret { get; set; } = string.Empty;
}
