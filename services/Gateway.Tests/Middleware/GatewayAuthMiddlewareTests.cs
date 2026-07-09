using System.Net;
using Gateway.Config;
using Gateway.Middleware;
using Gateway.Security;
using Gateway.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Gateway.Tests.Middleware;

public sealed class GatewayAuthMiddlewareTests
{
    [Theory]
    [InlineData("/api/login", "POST")]
    [InlineData("/api/join", "POST")]
    [InlineData("/api/join/nickname-available", "GET")]
    [InlineData("/health", "GET")]
    [InlineData("/metrics", "GET")]
    [InlineData("/api/users", "GET")]
    [InlineData("/api/users/42", "GET")]
    [InlineData("/api/posts", "GET")]
    [InlineData("/api/posts/7", "GET")]
    [InlineData("/api/comments", "GET")]
    [InlineData("/api/comments/3", "GET")]
    [InlineData("/api/media/9/content", "GET")]
    public async Task Invoke_AllowsAnonymousPaths_WithoutToken(string path, string method)
    {
        var context = CreateContext(path, method);
        var nextCalled = false;

        await CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.False(context.Request.Headers.ContainsKey("X-User-Id"));
        Assert.False(context.Request.Headers.ContainsKey("X-Gateway-Secret"));
    }

    [Theory]
    [InlineData("/api/posts", "POST")]
    [InlineData("/api/users", "PATCH")]
    [InlineData("/api/chat/rooms", "GET")]
    [InlineData("/api/groups", "GET")]
    public async Task Invoke_Returns401_WhenTokenMissingOnProtectedPath(string path, string method)
    {
        var context = CreateContext(path, method);

        await CreateMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_Returns401_WhenTokenInvalid()
    {
        var context = CreateContext("/api/posts", "POST");
        context.Request.Headers.Authorization = "Bearer not-a-valid-jwt";

        await CreateMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_InjectsIdentityHeaders_WhenBearerTokenValid()
    {
        const long userId = 99;
        var context = CreateContext("/api/posts", "POST");
        context.Request.Headers.Authorization = $"Bearer {TestJwtFactory.CreateToken(userId)}";
        string? forwardedUserId = null;
        string? forwardedSecret = null;

        await CreateMiddleware(ctx =>
        {
            forwardedUserId = ctx.Request.Headers["X-User-Id"].ToString();
            forwardedSecret = ctx.Request.Headers["X-Gateway-Secret"].ToString();
            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        Assert.Equal(userId.ToString(), forwardedUserId);
        Assert.Equal(TestWebHostConfiguration.GatewaySecret, forwardedSecret);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_AcceptsAccessTokenQueryParameter()
    {
        const long userId = 12;
        var context = CreateContext("/api/chat/rooms", "GET");
        context.Request.QueryString = new QueryString($"?access_token={TestJwtFactory.CreateToken(userId)}");
        string? forwardedUserId = null;

        await CreateMiddleware(ctx =>
        {
            forwardedUserId = ctx.Request.Headers["X-User-Id"].ToString();
            return Task.CompletedTask;
        }).InvokeAsync(context);

        Assert.Equal(userId.ToString(), forwardedUserId);
    }

    [Fact]
    public async Task Invoke_StripsIncomingIdentityHeaders_BeforeAnonymousPassThrough()
    {
        var context = CreateContext("/api/posts", "GET");
        context.Request.Headers["X-User-Id"] = "spoofed";
        context.Request.Headers["X-Gateway-Secret"] = "spoofed-secret";
        var nextSawUserId = false;
        var nextSawSecret = false;

        await CreateMiddleware(ctx =>
        {
            nextSawUserId = ctx.Request.Headers.ContainsKey("X-User-Id");
            nextSawSecret = ctx.Request.Headers.ContainsKey("X-Gateway-Secret");
            return Task.CompletedTask;
        }).InvokeAsync(context);

        Assert.False(nextSawUserId);
        Assert.False(nextSawSecret);
    }

    private static DefaultHttpContext CreateContext(string path, string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static GatewayAuthMiddleware CreateMiddleware(RequestDelegate next)
    {
        var gatewayOptions = Options.Create(new GatewayOptions
        {
            Secret = TestWebHostConfiguration.GatewaySecret,
        });
        var jwtOptions = Options.Create(new JwtOptions
        {
            Secret = TestWebHostConfiguration.JwtSecret,
            Issuer = TestWebHostConfiguration.JwtIssuer,
            Audience = TestWebHostConfiguration.JwtAudience,
        });
        return new GatewayAuthMiddleware(next, gatewayOptions, new JwtBearerValidator(jwtOptions));
    }
}
