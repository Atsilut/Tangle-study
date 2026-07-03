using Chat.Global.Config;
using Microsoft.Extensions.Options;

namespace Chat.Global.Telemetry;

public sealed class MetricsScrapeAuthMiddleware(RequestDelegate next, IOptions<MetricsOptions> metricsOptions)
{
    public const string HeaderName = "X-Metrics-Secret";

    private readonly MetricsOptions _metricsOptions = metricsOptions.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (_metricsOptions.RequireScrapeSecret
            && context.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase)
            && !IsAuthorized(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }

    private bool IsAuthorized(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_metricsOptions.ScrapeSecret))
            return false;

        return context.Request.Headers.TryGetValue(HeaderName, out var providedSecret)
            && providedSecret == _metricsOptions.ScrapeSecret;
    }
}
