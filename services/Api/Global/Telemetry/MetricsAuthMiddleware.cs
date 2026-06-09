using Api.Global.Config;
using Microsoft.Extensions.Options;

namespace Api.Global.Telemetry;

public sealed class MetricsAuthMiddleware(RequestDelegate next, IOptions<MetricsOptions> metricsOptions)
{
    public const string HeaderName = "X-Metrics-Secret";

    private readonly MetricsOptions _metricsOptions = metricsOptions.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (_metricsOptions.RequireAuth
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
        if (string.IsNullOrWhiteSpace(_metricsOptions.Secret))
            return false;

        return context.Request.Headers.TryGetValue(HeaderName, out var providedSecret)
            && providedSecret == _metricsOptions.Secret;
    }
}
