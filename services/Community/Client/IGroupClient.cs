namespace Community.Client;

public interface IGroupClient
{
    public Task EnsureCanViewBoardAsync(long groupId, long boardId, CancellationToken cancellationToken = default);

    public Task<bool> TryCanViewBoardAsync(long groupId, long boardId, CancellationToken cancellationToken = default);

    public Task EnsureCanWritePostAsync(long groupId, long boardId, CancellationToken cancellationToken = default);

    public Task<HashSet<(long GroupId, long BoardId)>> ResolveViewableBoardKeysAsync(
        IReadOnlyCollection<(long GroupId, long BoardId)> boardKeys,
        CancellationToken cancellationToken = default);
}
