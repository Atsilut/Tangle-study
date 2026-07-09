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
                ["Gateway:Secret"] = TestWebHostConfiguration.GatewaySecret,
                ["Jwt:Secret"] = TestWebHostConfiguration.JwtSecret,
                ["Jwt:Issuer"] = TestWebHostConfiguration.JwtIssuer,
                ["Jwt:Audience"] = TestWebHostConfiguration.JwtAudience,
                ["Metrics:RequireScrapeSecret"] = _metricsRequireScrapeSecret ? "true" : "false",
                ["Metrics:ScrapeSecret"] = _metricsScrapeSecret ?? "",
            });
        });
    }
}
