using Api.Client;

namespace Api.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="IChatClient"/> for Api integration tests (no chat-service container).
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    public List<long> DetachedUserIds { get; } = [];

    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default)
    {
        DetachedUserIds.Add(userId);
        return Task.CompletedTask;
    }
}
