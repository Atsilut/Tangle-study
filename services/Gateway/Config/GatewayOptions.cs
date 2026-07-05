namespace Gateway.Config;

public class GatewayOptions
{
    public const string SectionName = "Gateway";

    public string Secret { get; set; } = string.Empty;
}
