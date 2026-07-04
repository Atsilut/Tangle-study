namespace Api.Global.Config;

public sealed class GroupClientOptions
{
    public const string SectionName = "GroupClient";

    public string BaseUrl { get; set; } = string.Empty;

    public string InternalSecret { get; set; } = string.Empty;
}
