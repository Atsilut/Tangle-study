namespace Media.Global.Queue;

public interface IWorkQueue
{
    Task EnqueueAsync<TPayload>(string streamKey, TPayload payload, CancellationToken cancellationToken = default);
}
