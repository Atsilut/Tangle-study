using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Gateway.Tests.Infrastructure;

public sealed class GatewayWebApplicationFactory(
    bool metricsRequireScrapeSecret = false,
    string? metricsScrapeSecret = null) : WebApplicationFactory<Program>
{
    private readonly bool _metricsRequireScrapeSecret = metricsRequireScrapeSecret;
    private readonly string? _metricsScrapeSecret = metricsScrapeSecret;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Production);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:Secret"] = "test-gateway-secret",
                ["Jwt:Secret"] = "integration-test-jwt-secret-at-least-32-characters-long",
                ["Jwt:Issuer"] = "Tangle",
                ["Jwt:Audience"] = "TangleClient",
                ["Metrics:RequireScrapeSecret"] = _metricsRequireScrapeSecret ? "true" : "false",
                ["Metrics:ScrapeSecret"] = _metricsScrapeSecret ?? "",
            });
        });
    }
}
