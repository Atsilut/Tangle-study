namespace Api.Tests.Infrastructure;

/// <summary>
/// Base class for HTTP integration tests. Tests use explicit
/// <c>// Arrange</c>, <c>// Act</c>, and <c>// Assert</c> sections.
/// Use <c>// Act &amp; Assert</c> when the step is assertion-only (for example
/// <c>Assert.ThrowsAsync</c>). Unit tests in <c>Services/</c> follow the same convention.
/// Matrix tests place scenario setup in Arrange and branched outcome checks in Assert.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected ApiWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }

    protected FakeMediaClient FakeMediaClient => Factory.FakeMediaClient;

    protected IntegrationTestBase(
        PostgresTestcontainerFixture postgres,
        bool redisEnabled = false,
        string? redisConnectionString = null)
    {
        Factory = new ApiWebApplicationFactory(
            postgres.ConnectionString,
            redisEnabled,
            redisConnectionString);
        Client = Factory.CreateClient();
    }

    public async ValueTask InitializeAsync() => await Factory.ClearAllEntitiesAsync();

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
