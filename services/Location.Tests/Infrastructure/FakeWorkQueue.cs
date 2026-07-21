using Tangle.AspNetCore.Queue;

namespace Location.Tests.Infrastructure;

public sealed class FakeWorkQueue : IWorkQueue
{
    private readonly List<EnqueuedJob> _jobs = [];

    public Task EnqueueAsync<TPayload>(string streamKey, TPayload payload, CancellationToken cancellationToken = default)
    {
        _jobs.Add(new EnqueuedJob(streamKey, payload!));
        return Task.CompletedTask;
    }

    public Task EnqueueRawJsonAsync(string streamKey, string payloadJson, CancellationToken cancellationToken = default)
    {
        _jobs.Add(new EnqueuedJob(streamKey, payloadJson));
        return Task.CompletedTask;
    }

    public IReadOnlyList<EnqueuedJob> GetEnqueuedJobs() => _jobs;

    public sealed record EnqueuedJob(string StreamKey, object Payload);
}
