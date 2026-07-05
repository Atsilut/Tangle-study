using Location.Client;

namespace Location.Tests.Infrastructure;

public sealed class FakeSocialClient : ISocialClient
{
    public HashSet<(long Blocker, long Blocked)> Blocks { get; } = [];

    public void AddBlock(long blockerUserId, long blockedUserId) =>
        Blocks.Add((blockerUserId, blockedUserId));

    public void Reset() => Blocks.Clear();

    public Task<HashSet<long>> GetMutuallyBlockedUserIdsAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default)
    {
        var blocked = new HashSet<long>();
        foreach (var otherUserId in otherUserIds)
        {
            if (Blocks.Contains((userId, otherUserId)) || Blocks.Contains((otherUserId, userId)))
                blocked.Add(otherUserId);
        }

        return Task.FromResult(blocked);
    }

    public Task<bool> AnyBlockExistsBetweenUserAndOthersAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var otherUserId in otherUserIds)
        {
            if (Blocks.Contains((userId, otherUserId)) || Blocks.Contains((otherUserId, userId)))
                return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
