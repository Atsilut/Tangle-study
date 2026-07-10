using System.Net;

namespace Tangle.TestSupport.Integration;

public static class HealthCheck
{
    public static async Task WaitUntilReadyAsync(
        HttpClient client,
        string path = "health",
        TimeSpan? timeout = null,
        TimeSpan? retryDelay = null,
        bool throwOnTimeout = true)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        var delay = retryDelay ?? TimeSpan.FromMilliseconds(200);
        HttpStatusCode lastStatus = 0;
        var lastBody = string.Empty;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
                if (response.IsSuccessStatusCode)
                    return;

                lastStatus = response.StatusCode;
                lastBody = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(delay, TestContext.Current.CancellationToken);
        }

        if (!throwOnTimeout)
            return;

        var message = lastError is null
            ? $"API at {client.BaseAddress} did not become ready within {timeout}. Last status: {lastStatus}. Body: {lastBody}"
            : $"API at {client.BaseAddress} did not become ready within {timeout}.";
        throw new TimeoutException(message, lastError);
    }
}
