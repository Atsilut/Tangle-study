using System.Net;

namespace Gateway.Tests.Infrastructure;

internal static class IntegrationAssertions
{
    public static async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode == expected) return;

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Fail($"Expected {expected} but got {response.StatusCode}. Body: {body}");
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
