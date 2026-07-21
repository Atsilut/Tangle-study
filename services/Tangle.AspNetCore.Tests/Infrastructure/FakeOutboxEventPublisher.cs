using Tangle.AspNetCore.Outbox;

namespace Tangle.AspNetCore.Tests.Infrastructure;

public sealed class FakeOutboxEventPublisher : IOutboxEventPublisher
{
    private readonly List<(string Channel, string PayloadJson)> _published = [];
    private Exception? _nextFailure;
    private int _failuresRemaining;

    public IReadOnlyList<(string Channel, string PayloadJson)> Published => _published;

    public void FailNext(Exception exception, int times = 1)
    {
        _nextFailure = exception;
        _failuresRemaining = times;
    }

    public Task PublishAsync(string channel, string payloadJson, CancellationToken cancellationToken = default)
    {
        if (_failuresRemaining > 0 && _nextFailure is not null)
        {
            _failuresRemaining--;
            var failure = _nextFailure;
            if (_failuresRemaining == 0) _nextFailure = null;
            throw failure;
        }

        _published.Add((channel, payloadJson));
        return Task.CompletedTask;
    }
}
