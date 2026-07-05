using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace Tangle.TestSupport;

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

    public static async Task AssertHealthOkAsync(HttpClient client, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        HttpStatusCode lastStatus = 0;
        var lastBody = string.Empty;

        while (DateTime.UtcNow < deadline)
        {
            using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
            if (response.StatusCode == HttpStatusCode.OK)
                return;

            lastStatus = response.StatusCode;
            lastBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }

        Assert.Fail($"Expected OK but got {lastStatus}. Body: {lastBody}");
    }
}
