using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Chat.Tests.Infrastructure;

internal sealed class FakeHttpContextAccessor(string userId) : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = CreateContext(userId);

    private static DefaultHttpContext CreateContext(string userId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", userId)],
            authenticationType: "Test"));
        return context;
    }
}
