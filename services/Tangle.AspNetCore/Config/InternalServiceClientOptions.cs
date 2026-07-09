namespace Tangle.AspNetCore.Config;

public class InternalServiceClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public string InternalSecret { get; set; } = string.Empty;
}
