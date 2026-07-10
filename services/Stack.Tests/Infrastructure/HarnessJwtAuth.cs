using Tangle.TestSupport.Auth;

namespace Stack.Tests.Infrastructure;

public sealed class HarnessJwtAuth(string? password = null) : ITestAuth
{
    public static HarnessJwtAuth Instance { get; } = new();

    public Task AuthenticateAsync(HttpClient client, long userId, CancellationToken cancellationToken = default) =>
        HarnessAuthHelpers.LoginAsAsync(client, userId, password);
}
