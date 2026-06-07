using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests.Infrastructure;

internal static class IntegrationAssertions
{
    public static void AssertStatus(HttpResponseMessage response, HttpStatusCode expected) =>
        Assert.Equal(expected, response.StatusCode);

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
        Assert.Equal(expectedDetail, problem!.Detail);
    }
}
