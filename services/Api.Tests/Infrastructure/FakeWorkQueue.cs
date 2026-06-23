using Api.Global.Queue;

namespace Api.Tests.Infrastructure;

internal sealed class FakeWorkQueue : IWorkQueue
{
    private readonly List<EnqueuedJob> _jobs = [];

    public Task EnqueueAsync<TPayload>(string streamKey, TPayload payload, CancellationToken cancellationToken = default)
    {
        _jobs.Add(new EnqueuedJob(streamKey, payload!));
        return Task.CompletedTask;
    }

    public IReadOnlyList<EnqueuedJob> GetEnqueuedJobs() => _jobs;

    internal sealed record EnqueuedJob(string StreamKey, object Payload);
}
