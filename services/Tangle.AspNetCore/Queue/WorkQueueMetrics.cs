using Prometheus;

namespace Tangle.AspNetCore.Queue;

internal static class WorkQueueMetrics
{
    internal static readonly Counter EnqueueTotal = Metrics.CreateCounter(
        "tangle_workqueue_enqueue_total",
        "Jobs successfully enqueued to Redis Streams.",
        new CounterConfiguration { LabelNames = ["stream"] });

    internal static readonly Counter EnqueueFailedTotal = Metrics.CreateCounter(
        "tangle_workqueue_enqueue_failed_total",
        "Jobs that failed to enqueue to Redis Streams.",
        new CounterConfiguration { LabelNames = ["stream"] });
}
