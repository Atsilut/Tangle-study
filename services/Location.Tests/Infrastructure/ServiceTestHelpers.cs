using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Location.Tests.Infrastructure;

internal static class ServiceTestHelpers
{
    public static DefaultHttpContext ContextFor(long userId) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())])),
    };

    public static TestUser CreateUser(InMemoryUserClient monolith, string nickname = "test") =>
        monolith.CreateUser(nickname);
}
