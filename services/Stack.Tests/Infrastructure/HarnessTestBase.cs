using Tangle.TestSupport.Harness;

namespace Stack.Tests.Infrastructure;

public abstract class HarnessTestBase : IAsyncLifetime, IAsyncDisposable
{
    protected HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Assert.SkipUnless(
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(HarnessClientFactory.ApiBaseUrlEnv)),
            $"{HarnessClientFactory.ApiBaseUrlEnv} is not set.");

        Client = HarnessClientFactory.CreateClient();
        await HarnessClientFactory.WaitForApiReadyAsync(Client, TimeSpan.FromSeconds(90));
    }

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }

    protected static string UniqueTestPrefix(string testMethodName) =>
        $"{testMethodName}_{Guid.NewGuid():N}"[..Math.Min(40, testMethodName.Length + 9)];
}
