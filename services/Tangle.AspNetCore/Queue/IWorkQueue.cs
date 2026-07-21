namespace Tangle.AspNetCore.Queue;

public interface IWorkQueue
{
    Task EnqueueAsync<TPayload>(string streamKey, TPayload payload, CancellationToken cancellationToken = default);

    Task EnqueueRawJsonAsync(string streamKey, string payloadJson, CancellationToken cancellationToken = default);
}
