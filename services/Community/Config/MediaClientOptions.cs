namespace Community.Config;

public class MediaClientOptions
{
    public const string SectionName = "MediaClient";

    public string BaseUrl { get; set; } = string.Empty;

    public string InternalSecret { get; set; } = string.Empty;
}
