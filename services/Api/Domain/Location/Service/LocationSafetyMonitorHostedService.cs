using Api.Domain.Location.Config;
using Microsoft.Extensions.Options;

namespace Api.Domain.Location.Service;

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
                var service = scope.ServiceProvider.GetRequiredService<LocationSafetyAlertService>();
                await service.EvaluateStaleSessionsAsync();
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
