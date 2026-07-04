using Social.Client;
using Social.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Social.Tests.Infrastructure;

public sealed class InMemoryMonolithAccessClient : IMonolithAccessClient
{
    private long _nextUserId = 1;

    public HashSet<long> Users { get; } = [];
    public Dictionary<long, string> Nicknames { get; } = [];
    public Dictionary<long, FriendsListVisibility> FriendsListVisibilityByUserId { get; } = [];

    public long SeedUser(string nickname, long? userId = null, FriendsListVisibility visibility = FriendsListVisibility.Private)
    {
        var id = userId ?? _nextUserId++;
        if (userId is not null && userId.Value >= _nextUserId)
            _nextUserId = userId.Value + 1;
        Users.Add(id);
        Nicknames[id] = nickname;
        FriendsListVisibilityByUserId[id] = visibility;
        return id;
    }

    public void Reset()
    {
        Users.Clear();
        Nicknames.Clear();
        FriendsListVisibilityByUserId.Clear();
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

    public Task<FriendsListVisibility> GetFriendsListVisibilityAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        if (!Users.Contains(userId))
            throw new EntityNotFoundException("User not found");
        return Task.FromResult(FriendsListVisibilityByUserId.GetValueOrDefault(userId, FriendsListVisibility.Private));
    }
}
