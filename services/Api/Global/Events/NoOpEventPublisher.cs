namespace Api.Global.Events;

public sealed class NoOpEventPublisher : IEventPublisher
{
    public Task PublishAsync<TPayload>(string channel, TPayload payload, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
