using Azure.Monitor.OpenTelemetry.AspNetCore;

namespace Tangle.AspNetCore.Telemetry;

public static class AzureMonitorTelemetryExtensions
{
    /// <summary>
    /// Enables Azure Monitor OpenTelemetry when <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is set.
    /// </summary>
    public static WebApplicationBuilder AddTangleAzureMonitor(this WebApplicationBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
            return builder;

        builder.Services.AddOpenTelemetry().UseAzureMonitor();
        return builder;
    }
}
