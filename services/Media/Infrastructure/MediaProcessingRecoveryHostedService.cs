using Media.Config;
using Media.Service;
using Microsoft.Extensions.Options;

namespace Media.Infrastructure;

public sealed class MediaProcessingRecoveryHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<MediaProcessingRecoveryOptions> options,
    ILogger<MediaProcessingRecoveryHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly MediaProcessingRecoveryOptions _options = options.Value;
    private readonly ILogger<MediaProcessingRecoveryHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.IntervalSeconds <= 0)
        {
            _logger.LogInformation("Media processing recovery is disabled (IntervalSeconds <= 0).");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(15, _options.IntervalSeconds));
        var reenqueueAfter = TimeSpan.FromSeconds(Math.Max(30, _options.ReenqueueAfterSeconds));
        var failAfter = TimeSpan.FromSeconds(Math.Max(
            (int)reenqueueAfter.TotalSeconds + 30,
            _options.FailAfterSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                using var scope = _scopeFactory.CreateScope();
                var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
                var (reenqueued, failed) = await mediaService.RecoverStuckProcessingAsync(
                    reenqueueAfter,
                    failAfter,
                    _options.BatchSize,
                    stoppingToken);
                if (reenqueued > 0 || failed > 0)
                    _logger.LogWarning(
                        "Media processing recovery reenqueued {Reenqueued} and failed {Failed} assets",
                        reenqueued,
                        failed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Media processing recovery tick failed.");
            }
        }
    }
}
