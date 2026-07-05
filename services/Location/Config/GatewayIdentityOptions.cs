namespace Location.Config;

public class GatewayIdentityOptions
{
    public const string SectionName = "GatewayIdentity";

    public string Secret { get; set; } = string.Empty;
}
