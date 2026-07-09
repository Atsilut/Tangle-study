using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Tangle.TestSupport.Auth;

namespace Tangle.TestSupport.Integration;

public sealed class IntegrationTestOptions
{
    public required string ConnectionString { get; init; }
    public string? RedisConnectionString { get; init; }
    public string Environment { get; init; } = Environments.Production;
    public Dictionary<string, string?> AdditionalSettings { get; init; } = [];
}

public static class IntegrationTestConfiguration
{
    public static void Apply(IWebHostBuilder builder, IntegrationTestOptions options)
    {
        builder.UseEnvironment(options.Environment);

        var settings = BuildSettings(options);
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
    }

    public static Dictionary<string, string?> BuildSettings(IntegrationTestOptions options)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = options.ConnectionString,
            ["GatewayIdentity:Secret"] = TestWebHostConfiguration.GatewaySecret,
            ["Users:BaseUrl"] = "http://users.test",
            ["Users:InternalSecret"] = TestWebHostConfiguration.InternalServiceSecret,
            ["InternalAccess:Secret"] = TestWebHostConfiguration.InternalServiceSecret,
            ["Jwt:Secret"] = TestWebHostConfiguration.JwtSecret,
            ["Jwt:Issuer"] = TestWebHostConfiguration.JwtIssuer,
            ["Jwt:Audience"] = TestWebHostConfiguration.JwtAudience,
            ["Metrics:RequireScrapeSecret"] = "false",
        };

        if (options.RedisConnectionString is not null)
        {
            settings["Redis:ConnectionString"] = options.RedisConnectionString;
            settings["Redis:WorkQueueStreamPrefix"] = "tangle:queue:";
            settings["Redis:SignalRChannelPrefix"] = "tangle:signalr:";
        }

        foreach (var (key, value) in options.AdditionalSettings)
            settings[key] = value;

        return settings;
    }

    public static void AddDownstreamClient(Dictionary<string, string?> settings, string clientName, string baseUrl)
    {
        settings[$"{clientName}:BaseUrl"] = baseUrl;
        settings[$"{clientName}:InternalSecret"] = TestWebHostConfiguration.InternalServiceSecret;
    }
}
