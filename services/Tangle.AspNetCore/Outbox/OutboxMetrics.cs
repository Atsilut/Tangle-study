using Prometheus;

namespace Tangle.AspNetCore.Outbox;

internal static class OutboxMetrics
{
    internal static readonly Counter DispatchedTotal = Metrics.CreateCounter(
        "tangle_outbox_dispatched_total",
        "Outbox rows successfully dispatched to Redis Streams / pub/sub.",
        new CounterConfiguration { LabelNames = ["destination"] });

    internal static readonly Counter DeadLetteredTotal = Metrics.CreateCounter(
        "tangle_outbox_dead_lettered_total",
        "Outbox rows dead-lettered after exhausting retries.",
        new CounterConfiguration { LabelNames = ["destination"] });

    internal static readonly Counter PrunedTotal = Metrics.CreateCounter(
        "tangle_outbox_pruned_total",
        "Processed outbox rows deleted by retention pruning.");
}
