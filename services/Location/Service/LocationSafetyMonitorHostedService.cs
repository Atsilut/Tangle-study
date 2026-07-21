using Location.Config;
using Microsoft.Extensions.Options;

namespace Location.Service;

public sealed class LocationSafetyMonitorHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<LocationSafetyOptions> options,
    ILogger<LocationSafetyMonitorHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly LocationSafetyOptions _options = options.Value;
    private readonly ILogger<LocationSafetyMonitorHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(15, _options.MonitorIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                using var scope = _scopeFactory.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<LocationSessionService>();
                await sessionService.ReconcileGhostSessionsAsync();
                var mapPinService = scope.ServiceProvider.GetRequiredService<MapPinService>();
                var orphanPins = await mapPinService.ReconcileOrphanPostPinsAsync(batchSize: 100, stoppingToken);
                if (orphanPins > 0)
                    _logger.LogWarning("Location orphan pin reconciliation removed {Count} pins", orphanPins);
                var safetyService = scope.ServiceProvider.GetRequiredService<LocationSafetyAlertService>();
                await safetyService.EvaluateStaleSessionsAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Location safety monitor tick failed.");
            }
        }
    }
}
