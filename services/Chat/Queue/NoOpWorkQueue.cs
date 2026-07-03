namespace Chat.Queue;

public sealed class NoOpWorkQueue : IWorkQueue
{
    public Task EnqueueAsync<TPayload>(string streamKey, TPayload payload, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
