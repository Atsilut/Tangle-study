namespace Chat.Events;

public interface IEventPublisher
{
    public Task PublishAsync<TPayload>(string channel, TPayload payload, CancellationToken cancellationToken = default);
}
