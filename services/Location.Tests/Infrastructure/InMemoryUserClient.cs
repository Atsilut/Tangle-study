using Location.Client;

namespace Location.Tests.Infrastructure;

public sealed class InMemoryUserClient : IUserClient
{
    private long _nextUserId = 1;

    public HashSet<long> Users { get; } = [];
    public Dictionary<long, string> Nicknames { get; } = [];

    public TestUser CreateUser(string nickname)
    {
        var id = _nextUserId++;
        Users.Add(id);
        Nicknames[id] = nickname;
        return new TestUser(id, nickname);
    }

    public void Reset()
    {
        Users.Clear();
        Nicknames.Clear();
        _nextUserId = 1;
    }

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (!Users.Contains(userId))
            throw new ArgumentException("User not found");
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
}

public sealed record TestUser(long Id, string Nickname);
