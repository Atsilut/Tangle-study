using Users.Client;

namespace Users.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="ICommunityClient"/> for Users integration tests (no community-service container).
/// </summary>
public sealed class FakeCommunityClient : ICommunityClient
{
    public List<long> DetachedUserIds { get; } = [];
    public List<long> DeletedGroupIds { get; } = [];
    public Exception? DetachFailure { get; set; }

    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (DetachFailure is not null) throw DetachFailure;
        DetachedUserIds.Add(userId);
        return Task.CompletedTask;
    }

    public Task DeleteAllByGroupAsync(long groupId, CancellationToken cancellationToken = default)
    {
        DeletedGroupIds.Add(groupId);
        return Task.CompletedTask;
    }
}
