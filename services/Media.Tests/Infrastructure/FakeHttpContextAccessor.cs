using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Media.Tests.Infrastructure;

internal sealed class FakeHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }

    public FakeHttpContextAccessor(string userId)
    {
        var claims = new[] { new Claim("sub", userId) };
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims)),
        };
    }
}
