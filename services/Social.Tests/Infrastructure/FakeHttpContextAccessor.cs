using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Social.Tests.Infrastructure;

public sealed class FakeHttpContextAccessor : IHttpContextAccessor
{
    public FakeHttpContextAccessor(string userId)
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim("sub", userId)], authenticationType: "Test")),
        };
    }

    public HttpContext? HttpContext { get; set; }
}
