using Api.Client;

namespace Api.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="ICommunityClient"/> for Api integration tests (no community-service container).
/// </summary>
public sealed class FakeCommunityClient : ICommunityClient
{
    public List<long> DetachedUserIds { get; } = [];
    public List<long> DeletedGroupIds { get; } = [];

    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default)
    {
        DetachedUserIds.Add(userId);
        return Task.CompletedTask;
    }

    public Task DeleteAllByGroupAsync(long groupId, CancellationToken cancellationToken = default)
    {
        DeletedGroupIds.Add(groupId);
        return Task.CompletedTask;
    }
}
