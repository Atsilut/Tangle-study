namespace Api.Global.Events;

public interface IEventPublisher
{
    Task PublishAsync<TPayload>(string channel, TPayload payload, CancellationToken cancellationToken = default);
}
