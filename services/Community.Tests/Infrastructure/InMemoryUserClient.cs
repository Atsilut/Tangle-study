using Community.Client;
using Community.Exceptions;

namespace Community.Tests.Infrastructure;

public sealed class InMemoryUserClient : IUserClient, ISocialClient
{
    public HashSet<long> Users { get; } = [];
    public Dictionary<long, string> Nicknames { get; } = [];
    public HashSet<(long UserId, long OtherUserId)> MutualBlocks { get; } = [];

    public long SeedUser(string nickname, long? userId = null)
    {
        var id = userId ?? (Users.Count == 0 ? 1 : Users.Max() + 1);
        Users.Add(id);
        Nicknames[id] = nickname;
        return id;
    }

    public void Reset()
    {
        Users.Clear();
        Nicknames.Clear();
        MutualBlocks.Clear();
    }

    /// <summary>
    /// Simulates monolith user deletion so nickname resolution falls back to "Deleted User".
    /// </summary>
    public void SimulateUserDeleted(long userId)
    {
        Users.Remove(userId);
        Nicknames.Remove(userId);
    }

    public void AddMutualBlock(long userId, long otherUserId) =>
        MutualBlocks.Add((userId, otherUserId));

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (!Users.Contains(userId))
            throw new EntityNotFoundException("User not found", statusCode: 400);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default)
    {
        var result = userIds
            .Distinct()
            .ToDictionary(id => id, id => Nicknames.GetValueOrDefault(id, "Deleted User"));
        return Task.FromResult<IReadOnlyDictionary<long, string>>(result);
    }

    public Task<long?> GetUserIdByNicknameAsync(string nickname, CancellationToken cancellationToken = default)
    {
        foreach (var (id, name) in Nicknames)
        {
            if (name == nickname) return Task.FromResult<long?>(id);
        }

        return Task.FromResult<long?>(null);
    }

    public Task<HashSet<long>> GetMutuallyBlockedUserIdsAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default)
    {
        HashSet<long> blocked = [];
        foreach (var other in otherUserIds)
        {
            if (MutualBlocks.Contains((userId, other)) || MutualBlocks.Contains((other, userId)))
                blocked.Add(other);
        }

        return Task.FromResult(blocked);
    }
}
