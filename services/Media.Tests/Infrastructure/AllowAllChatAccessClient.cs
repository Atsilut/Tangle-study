using Media.Client;

namespace Media.Tests.Infrastructure;

internal sealed class AllowAllChatAccessClient : IChatAccessClient
{
    public Task EnsureCanAccessChatMessageMediaAsync(long chatMessageId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
