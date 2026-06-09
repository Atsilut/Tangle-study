using System.Net;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class MetricsIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task Metrics_ReturnsOk_WithPrometheusHttpMetrics()
    {
        await Client.GetAsync("/api/users", TestContext.Current.CancellationToken);

        var response = await Client.GetAsync("/metrics", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("http_requests_received_total", body, StringComparison.Ordinal);
        Assert.Contains("http_request_duration_seconds", body, StringComparison.Ordinal);
    }
}
