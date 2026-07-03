namespace Api.Global.Config;

public class ChatClientOptions
{
    public const string SectionName = "ChatClient";

    public string BaseUrl { get; set; } = string.Empty;

    public string InternalSecret { get; set; } = string.Empty;
}
