using Tangle.AspNetCore.Queue;

namespace Media.Tests.Infrastructure;

public sealed class FakeWorkQueue : IWorkQueue
{
    private readonly List<EnqueuedJob> _jobs = [];

    public Task EnqueueAsync<TPayload>(string streamKey, TPayload payload, CancellationToken cancellationToken = default)
    {
        _jobs.Add(new EnqueuedJob(streamKey, payload!));
        return Task.CompletedTask;
    }

    public IReadOnlyList<EnqueuedJob> GetEnqueuedJobs() => _jobs;

    public sealed record EnqueuedJob(string StreamKey, object Payload);
}
