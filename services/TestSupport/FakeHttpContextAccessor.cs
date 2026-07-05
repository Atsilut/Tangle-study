using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Tangle.TestSupport;

public sealed class FakeHttpContextAccessor(string userId) : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; } = CreateContext(userId);

    public static DefaultHttpContext ContextFor(long userId) => CreateContext(userId.ToString());

    private static DefaultHttpContext CreateContext(string userId)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", userId)],
            authenticationType: "Test"));
        return context;
    }
}
