using Group.Client;
using Group.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Group.Tests.Infrastructure;

public sealed class InMemoryMonolithAccessClient : IMonolithAccessClient, ISocialClient
{
    private long _nextUserId = 1;

    public HashSet<long> Users { get; } = [];
    public Dictionary<long, string> Nicknames { get; } = [];
    public HashSet<(long Blocker, long Blocked)> Blocks { get; } = [];

    public long SeedUser(string nickname, long? userId = null)
    {
        var id = userId ?? _nextUserId++;
        if (userId is not null && userId.Value >= _nextUserId)
            _nextUserId = userId.Value + 1;
        Users.Add(id);
        Nicknames[id] = nickname;
        return id;
    }

    public void AddBlock(long blockerUserId, long blockedUserId) =>
        Blocks.Add((blockerUserId, blockedUserId));

    public void Reset()
    {
        Users.Clear();
        Nicknames.Clear();
        Blocks.Clear();
        _nextUserId = 1;
    }

    public Task EnsureUserExistsAsync(
        long userId,
        string notFoundMessage = "User not found",
        int statusCode = StatusCodes.Status400BadRequest,
        CancellationToken cancellationToken = default)
    {
        if (!Users.Contains(userId))
            throw new EntityNotFoundException(notFoundMessage, statusCode);
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

    public Task<bool> IsBlockedByAsync(
        long blockerUserId,
        long blockedUserId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Blocks.Contains((blockerUserId, blockedUserId)));
}
