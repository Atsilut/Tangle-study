using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Users.Tests.Repositories;

public class FakeHttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get; set; }

    public FakeHttpContextAccessor(string userId)
    {
        var claims = new[] { new Claim("sub", userId) };
        var identity = new ClaimsIdentity(claims);
        var claimsPrincipal = new ClaimsPrincipal(identity);

        HttpContext = new DefaultHttpContext
        {
            User = claimsPrincipal
        };
    }
}
