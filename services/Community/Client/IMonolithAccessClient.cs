namespace Community.Client;

public interface IMonolithAccessClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default);

    public Task<long?> GetUserIdByNicknameAsync(string nickname, CancellationToken cancellationToken = default);

    public Task<HashSet<long>> GetMutuallyBlockedUserIdsAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default);

    public Task EnsureCanViewBoardAsync(long groupId, long boardId, CancellationToken cancellationToken = default);

    public Task<bool> TryCanViewBoardAsync(long groupId, long boardId, CancellationToken cancellationToken = default);

    public Task EnsureCanWritePostAsync(long groupId, long boardId, CancellationToken cancellationToken = default);

    public Task<HashSet<(long GroupId, long BoardId)>> ResolveViewableBoardKeysAsync(
        IReadOnlyCollection<(long GroupId, long BoardId)> boardKeys,
        CancellationToken cancellationToken = default);
}
