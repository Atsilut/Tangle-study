using Microsoft.AspNetCore.Mvc.Testing;

namespace Tangle.TestSupport.Integration;

public abstract class IntegrationTestBase<TFactory, TEntryPoint> : IAsyncLifetime
    where TFactory : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    protected TFactory Factory { get; }
    protected HttpClient Client { get; }

    protected IntegrationTestBase(Func<TFactory> createFactory, Action<TFactory>? afterCreate = null)
    {
        Factory = createFactory();
        afterCreate?.Invoke(Factory);
        Client = Factory.CreateClient();
    }

    protected abstract ValueTask ResetStateAsync();

    public async ValueTask InitializeAsync() => await ResetStateAsync();

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
