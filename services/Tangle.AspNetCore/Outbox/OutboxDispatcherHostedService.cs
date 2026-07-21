using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Queue;

namespace Tangle.AspNetCore.Outbox;

/// <summary>
/// Optional pub/sub sink for outbox rows with <see cref="OutboxDestination.Event"/>.
/// Register a no-op or service-specific publisher when events are used.
/// </summary>
public interface IOutboxEventPublisher
{
    public Task PublishAsync(string channel, string payloadJson, CancellationToken cancellationToken = default);
}

public sealed class NoOpOutboxEventPublisher : IOutboxEventPublisher
{
    public Task PublishAsync(string channel, string payloadJson, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class OutboxDispatcherHostedService<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcherHostedService<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly OutboxOptions _options = options.Value;
    private readonly ILogger<OutboxDispatcherHostedService<TContext>> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(200, _options.PollIntervalMilliseconds));
        var batchSize = Math.Clamp(_options.BatchSize, 1, 500);
        var maxAttempts = Math.Max(1, _options.MaxAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await DispatchBatchAsync(batchSize, maxAttempts, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatcher tick failed for {DbContext}", typeof(TContext).Name);
            }
        }
    }

    private async Task DispatchBatchAsync(int batchSize, int maxAttempts, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var workQueue = scope.ServiceProvider.GetRequiredService<IWorkQueue>();
        var eventPublisher = scope.ServiceProvider.GetService<IOutboxEventPublisher>()
            ?? scope.ServiceProvider.GetRequiredService<NoOpOutboxEventPublisher>();

        var pending = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null && m.DeadLetteredAt == null)
            .OrderBy(m => m.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0) return;

        foreach (var message in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (message.Destination == OutboxDestination.WorkQueue)
                    await workQueue.EnqueueRawJsonAsync(message.Target, message.PayloadJson, cancellationToken);
                else
                    await eventPublisher.PublishAsync(message.Target, message.PayloadJson, cancellationToken);

                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                if (message.Attempts >= maxAttempts)
                {
                    message.DeadLetteredAt = DateTimeOffset.UtcNow;
                    _logger.LogError(
                        ex,
                        "Outbox message {OutboxId} dead-lettered after {Attempts} attempts ({Target})",
                        message.Id,
                        message.Attempts,
                        message.Target);
                }
                else
                {
                    _logger.LogWarning(
                        ex,
                        "Outbox message {OutboxId} publish failed (attempt {Attempts}/{Max})",
                        message.Id,
                        message.Attempts,
                        maxAttempts);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
