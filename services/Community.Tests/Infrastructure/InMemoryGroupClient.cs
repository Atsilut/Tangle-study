using Community.Client;
using Community.Exceptions;

namespace Community.Tests.Infrastructure;

public sealed class InMemoryGroupClient : IGroupClient
{
    public HashSet<(long GroupId, long BoardId)> ViewableBoards { get; } = [];
    public HashSet<(long GroupId, long BoardId)> WritableBoards { get; } = [];

    public bool AllowAllBoards { get; set; } = true;

    public void Reset()
    {
        ViewableBoards.Clear();
        WritableBoards.Clear();
        AllowAllBoards = true;
    }

    public void AllowBoard(long groupId, long boardId, bool writable = true)
    {
        AllowAllBoards = false;
        ViewableBoards.Add((groupId, boardId));
        if (writable) WritableBoards.Add((groupId, boardId));
    }

    public Task EnsureCanViewBoardAsync(long groupId, long boardId, CancellationToken cancellationToken = default)
    {
        if (AllowAllBoards || ViewableBoards.Contains((groupId, boardId)))
            return Task.CompletedTask;
        throw new AccessForbiddenException("Unauthorized access");
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
        catch (AccessForbiddenException)
        {
            return false;
        }
    }

    public Task EnsureCanWritePostAsync(long groupId, long boardId, CancellationToken cancellationToken = default)
    {
        if (AllowAllBoards || WritableBoards.Contains((groupId, boardId)))
            return Task.CompletedTask;
        throw new AccessForbiddenException("Unauthorized access");
    }

    public Task<HashSet<(long GroupId, long BoardId)>> ResolveViewableBoardKeysAsync(
        IReadOnlyCollection<(long GroupId, long BoardId)> boardKeys,
        CancellationToken cancellationToken = default)
    {
        if (AllowAllBoards) return Task.FromResult(boardKeys.ToHashSet());
        return Task.FromResult(boardKeys.Where(ViewableBoards.Contains).ToHashSet());
    }
}
