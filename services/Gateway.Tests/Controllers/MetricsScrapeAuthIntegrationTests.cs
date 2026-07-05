using System.Net;
using Gateway.Telemetry;
using Gateway.Tests.Infrastructure;

namespace Gateway.Tests.Controllers;

/// <summary>
/// Scrape auth for <c>GET /metrics</c> via <see cref="MetricsScrapeAuthMiddleware"/> and
/// <c>X-Metrics-Secret</c>.
/// </summary>
public sealed class MetricsScrapeAuthIntegrationTests : IAsyncLifetime
{
    private const string TestMetricsSecret = "test-metrics-secret";

    private GatewayWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    public async Task MetricsScrapeAuth_Returns401_WhenSecretMissing()
    {
        var response = await _client.GetAsync("/metrics", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MetricsScrapeAuth_ReturnsOk_WhenSecretProvided()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/metrics");
        request.Headers.Add(MetricsScrapeAuthMiddleware.HeaderName, TestMetricsSecret);

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("http_requests_received_total", body, StringComparison.Ordinal);
    }

    public ValueTask InitializeAsync()
    {
        _factory = new GatewayWebApplicationFactory(
            metricsRequireScrapeSecret: true,
            metricsScrapeSecret: TestMetricsSecret);
        _client = _factory.CreateClient();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
