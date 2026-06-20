using System.Net;
using System.Net.Http.Json;
using Api.Domain.Users.Dto;
using Api.Tests.Infrastructure;

namespace Api.Tests.Controllers;

[Collection(IntegrationTestCollection.Name)]
public sealed class MetricsIntegrationTests(PostgresTestcontainerFixture postgres)
    : IntegrationTestBase(postgres)
{
    [Fact]
    public async Task Health_ReturnsOk_WhenDependenciesAreHealthy()
    {
        await IntegrationAssertions.AssertHealthOkAsync(Client);
    }

    [Fact]
    public async Task Metrics_ReturnsOk_WithPrometheusHttpMetrics()
    {
        await Client.GetAsync("/api/users", TestContext.Current.CancellationToken);

        var response = await Client.GetAsync("/metrics", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("http_requests_received_total", body, StringComparison.Ordinal);
        Assert.Contains("http_request_duration_seconds", body, StringComparison.Ordinal);
        Assert.Contains("controller=\"User\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Metrics_Records404_AfterNotFoundResponse()
    {
        // Arrange
        var before = await GetHttpRequestTotalForCodeAsync("404");

        // Act
        var response = await Client.GetAsync("/api/users/999999", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.NotFound);

        // Assert
        var after = await GetHttpRequestTotalForCodeAsync("404");
        Assert.True(after > before, $"Expected 404 counter to increase (before={before}, after={after}).");
    }

    [Fact]
    public async Task Metrics_Records401_AfterUnauthenticatedApiRequest()
    {
        // Arrange
        var before = await GetHttpRequestTotalForCodeAsync("401");

        // Act
        var response = await Client.PatchAsJsonAsync(
            "/api/users",
            new UserPatchRequestDto { Id = 1, Nickname = "metrics-test" },
            TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.Unauthorized);

        // Assert
        var after = await GetHttpRequestTotalForCodeAsync("401");
        Assert.True(after > before, $"Expected 401 counter to increase (before={before}, after={after}).");
    }

    private async Task<double> GetHttpRequestTotalForCodeAsync(string code)
    {
        var response = await Client.GetAsync("/metrics", TestContext.Current.CancellationToken);
        await IntegrationAssertions.AssertStatusAsync(response, HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return ParseHttpRequestTotalForCode(body, code);
    }

    private static double ParseHttpRequestTotalForCode(string metricsBody, string code)
    {
        var needle = $"code=\"{code}\"";
        var total = 0.0;

        foreach (var line in metricsBody.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.StartsWith("http_requests_received_total", StringComparison.Ordinal))
                continue;
            if (!line.Contains(needle, StringComparison.Ordinal))
                continue;

            var valueToken = line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];
            if (double.TryParse(valueToken, out var value))
                total += value;
        }

        return total;
    }
}
