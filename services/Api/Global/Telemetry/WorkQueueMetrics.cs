using Prometheus;

namespace Api.Global.Telemetry;

internal static class WorkQueueMetrics
{
    internal static readonly Counter EnqueueTotal = Metrics.CreateCounter(
        "tangle_workqueue_enqueue_total",
        "Jobs successfully enqueued to Redis Streams.",
        new CounterConfiguration { LabelNames = ["stream"] });
}
