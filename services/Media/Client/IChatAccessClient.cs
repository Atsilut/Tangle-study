namespace Media.Client;

public interface IChatAccessClient
{
    Task EnsureCanAccessChatMessageMediaAsync(long chatMessageId, CancellationToken cancellationToken = default);
}
