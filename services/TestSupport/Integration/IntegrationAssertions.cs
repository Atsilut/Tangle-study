using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace Tangle.TestSupport.Integration;

public static class IntegrationAssertions
{
    public static async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode == expected) return;

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Fail($"Expected {expected} but got {response.StatusCode}. Body: {body}");
    }

    public static async Task AssertProblemDetailContainsAsync(
        HttpResponseMessage response,
        string expectedSubstring)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.Contains(expectedSubstring, problem.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task AssertProblemDetailAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedDetail)
    {
        await AssertStatusAsync(response, expectedStatus);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(problem);
        Assert.NotNull(problem.Detail);
        Assert.Equal(expectedDetail, problem.Detail);
    }

    public static Task AssertHealthOkAsync(HttpClient client, TimeSpan? timeout = null) =>
        HealthCheck.WaitUntilReadyAsync(client, "/health", timeout);
}
