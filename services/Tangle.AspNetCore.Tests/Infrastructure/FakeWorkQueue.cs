using Tangle.AspNetCore.Queue;

namespace Tangle.AspNetCore.Tests.Infrastructure;

public sealed class FakeWorkQueue : IWorkQueue
{
    private readonly List<(string StreamKey, string PayloadJson)> _enqueued = [];
    private Exception? _nextFailure;
    private int _failuresRemaining;

    public IReadOnlyList<(string StreamKey, string PayloadJson)> Enqueued => _enqueued;

    public void FailNext(Exception exception, int times = 1)
    {
        _nextFailure = exception;
        _failuresRemaining = times;
    }

    public Task EnqueueAsync<TPayload>(string streamKey, TPayload payload, CancellationToken cancellationToken = default) =>
        EnqueueRawJsonAsync(streamKey, payload?.ToString() ?? string.Empty, cancellationToken);

    public Task EnqueueRawJsonAsync(string streamKey, string payloadJson, CancellationToken cancellationToken = default)
    {
        if (_failuresRemaining > 0 && _nextFailure is not null)
        {
            _failuresRemaining--;
            var failure = _nextFailure;
            if (_failuresRemaining == 0) _nextFailure = null;
            throw failure;
        }

        _enqueued.Add((streamKey, payloadJson));
        return Task.CompletedTask;
    }
}
