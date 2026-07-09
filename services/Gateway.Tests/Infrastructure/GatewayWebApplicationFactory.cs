using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Gateway.Tests.Infrastructure;

public sealed class GatewayWebApplicationFactory(
    bool metricsRequireScrapeSecret = false,
    string? metricsScrapeSecret = null,
    string? downstreamAddress = null) : WebApplicationFactory<Program>
{
    private readonly bool _metricsRequireScrapeSecret = metricsRequireScrapeSecret;
    private readonly string? _metricsScrapeSecret = metricsScrapeSecret;
    private readonly string? _downstreamAddress = downstreamAddress;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Gateway:Secret"] = TestWebHostConfiguration.GatewaySecret,
                ["Jwt:Secret"] = TestWebHostConfiguration.JwtSecret,
                ["Jwt:Issuer"] = TestWebHostConfiguration.JwtIssuer,
                ["Jwt:Audience"] = TestWebHostConfiguration.JwtAudience,
                ["Metrics:RequireScrapeSecret"] = _metricsRequireScrapeSecret ? "true" : "false",
                ["Metrics:ScrapeSecret"] = _metricsScrapeSecret ?? "",
            };

            if (!string.IsNullOrWhiteSpace(_downstreamAddress))
            {
                foreach (var cluster in new[]
                         {
                             "users", "media", "chat", "location", "community", "group", "social",
                         })
                    settings[$"ReverseProxy:Clusters:{cluster}:Destinations:d1:Address"] = _downstreamAddress;
            }

            config.AddInMemoryCollection(settings);
        });
    }
}
