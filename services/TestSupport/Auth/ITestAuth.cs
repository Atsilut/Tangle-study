using Tangle.TestSupport.Auth;

namespace Tangle.TestSupport.Auth;

public interface ITestAuth
{
    Task AuthenticateAsync(HttpClient client, long userId, CancellationToken cancellationToken = default);
}

public sealed class GatewayHeaderAuth : ITestAuth
{
    public static GatewayHeaderAuth Instance { get; } = new();

    public Task AuthenticateAsync(HttpClient client, long userId, CancellationToken cancellationToken = default)
    {
        GatewayTestAuthHelpers.LoginAs(client, userId);
        return Task.CompletedTask;
    }
}
