using Media.Config;
using Media.Service;
using Microsoft.Extensions.Options;

namespace Media.Infrastructure;

public sealed class MediaOrphanReconciliationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<MediaReconciliationOptions> options,
    ILogger<MediaOrphanReconciliationHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly MediaReconciliationOptions _options = options.Value;
    private readonly ILogger<MediaOrphanReconciliationHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.IntervalSeconds <= 0)
        {
            _logger.LogInformation("Media orphan reconciliation is disabled (IntervalSeconds <= 0).");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(30, _options.IntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                using var scope = _scopeFactory.CreateScope();
                var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
                var unlinked = await mediaService.ReconcileOrphanContentLinksAsync(
                    _options.BatchSize,
                    stoppingToken);
                if (unlinked > 0)
                    _logger.LogWarning("Media orphan reconciliation unlinked {Count} assets", unlinked);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Media orphan reconciliation tick failed.");
            }
        }
    }
}
