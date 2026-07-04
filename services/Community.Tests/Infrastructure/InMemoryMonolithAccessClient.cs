using Community.Client;
using Community.Exceptions;

namespace Community.Tests.Infrastructure;

public sealed class InMemoryMonolithAccessClient : IMonolithAccessClient
{
    public HashSet<long> Users { get; } = [];
    public Dictionary<long, string> Nicknames { get; } = [];
    public HashSet<(long UserId, long OtherUserId)> MutualBlocks { get; } = [];
    public HashSet<(long GroupId, long BoardId)> ViewableBoards { get; } = [];
    public HashSet<(long GroupId, long BoardId)> WritableBoards { get; } = [];

    public bool AllowAllBoards { get; set; } = true;

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
        ViewableBoards.Clear();
        WritableBoards.Clear();
        AllowAllBoards = true;
    }

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

    public Task EnsureCanViewBoardAsync(long groupId, long boardId, CancellationToken cancellationToken = default)
    {
        if (AllowAllBoards || ViewableBoards.Contains((groupId, boardId)))
            return Task.CompletedTask;
        throw new UnauthorizedAccessException("Unauthorized access");
    }

    public async Task<bool> TryCanViewBoardAsync(
        long groupId,
        long boardId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureCanViewBoardAsync(groupId, boardId, cancellationToken);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public Task EnsureCanWritePostAsync(long groupId, long boardId, CancellationToken cancellationToken = default)
    {
        if (AllowAllBoards || WritableBoards.Contains((groupId, boardId)))
            return Task.CompletedTask;
        throw new UnauthorizedAccessException("Unauthorized access");
    }

    public Task<HashSet<(long GroupId, long BoardId)>> ResolveViewableBoardKeysAsync(
        IReadOnlyCollection<(long GroupId, long BoardId)> boardKeys,
        CancellationToken cancellationToken = default)
    {
        if (AllowAllBoards) return Task.FromResult(boardKeys.ToHashSet());
        return Task.FromResult(boardKeys.Where(ViewableBoards.Contains).ToHashSet());
    }
}
