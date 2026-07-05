using System.Net;
using Gateway.Tests.Infrastructure;

namespace Gateway.Tests.Controllers;

public sealed class MetricsIntegrationTests : IAsyncLifetime
{
    private GatewayWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        await IntegrationAssertions.AssertHealthOkAsync(_client);
    }

    [Fact]
    public async Task Metrics_ReturnsOk_WithPrometheusHttpMetrics()
    {
        await _client.GetAsync("/health", TestContext.Current.CancellationToken);

        var response = await _client.GetAsync("/metrics", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("http_requests_received_total", body, StringComparison.Ordinal);
        Assert.Contains("http_request_duration_seconds", body, StringComparison.Ordinal);
    }

    public ValueTask InitializeAsync()
    {
        _factory = new GatewayWebApplicationFactory();
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
