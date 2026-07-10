using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Gateway.Tests.Infrastructure;

/// <summary>
/// Minimal downstream that echoes gateway identity headers for YARP integration tests.
/// </summary>
public sealed class DownstreamEchoServer : IAsyncDisposable
{
    private readonly WebApplication _app;

    public string BaseAddress { get; }

    private DownstreamEchoServer(WebApplication app, string baseAddress)
    {
        _app = app;
        BaseAddress = baseAddress;
    }

    public static async Task<DownstreamEchoServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.MapMethods(
            "{**path}",
            ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD"],
            async (HttpContext context) =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                context.Response.ContentType = "application/json";
                var userId = context.Request.Headers["X-User-Id"].ToString();
                var gatewaySecret = context.Request.Headers["X-Gateway-Secret"].ToString();
                await context.Response.WriteAsJsonAsync(new
                {
                    path = context.Request.Path.Value,
                    method = context.Request.Method,
                    userId,
                    gatewaySecret,
                });
            });

        await app.StartAsync();
        var address = app.Urls.First();
        if (!address.EndsWith('/'))
            address += "/";
        return new DownstreamEchoServer(app, address);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
