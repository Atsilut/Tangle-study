namespace Tangle.TestSupport.Integration;

public static class IntegrationTestPolling
{
    public static async Task<T> PollUntilAsync<T>(
        Func<CancellationToken, Task<T>> fetch,
        Func<T, bool> isDone,
        TimeSpan timeout,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        delay ??= TimeSpan.FromMilliseconds(250);
        var deadline = DateTime.UtcNow + timeout;
        T? last = default;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            last = await fetch(cancellationToken);
            if (isDone(last))
                return last;

            await Task.Delay(delay.Value, cancellationToken);
        }

        throw new TimeoutException($"Condition not met within {timeout}. Last value: {last}");
    }
}
