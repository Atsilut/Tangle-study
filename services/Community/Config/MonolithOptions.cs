namespace Community.Config;

public class MonolithOptions
{
    public const string SectionName = "Monolith";

    public string BaseUrl { get; set; } = string.Empty;

    public string InternalSecret { get; set; } = string.Empty;
}
