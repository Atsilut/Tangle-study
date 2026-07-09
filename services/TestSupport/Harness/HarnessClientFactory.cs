namespace Tangle.TestSupport.Harness;

public static class HarnessClientFactory
{
    public const string ApiBaseUrlEnv = "TANGLE_HARNESS_API_BASE_URL";

    public static HttpClient CreateClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable(ApiBaseUrlEnv)
            ?? throw new InvalidOperationException($"{ApiBaseUrlEnv} is not set.");
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(3),
        };
    }

    public static Task WaitForApiReadyAsync(HttpClient client, TimeSpan timeout) =>
        Integration.HealthCheck.WaitUntilReadyAsync(
            client,
            "health",
            timeout,
            TimeSpan.FromSeconds(1),
            throwOnTimeout: true);
}