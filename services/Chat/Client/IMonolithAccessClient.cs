namespace Chat.Client;

public interface IMonolithAccessClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default);

    public Task EnsureUsersExistAsync(IReadOnlyCollection<long> userIds, CancellationToken cancellationToken = default);

    public Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default);

    public Task EnsureFriendshipExistsForUserPairAsync(long otherUserId, CancellationToken cancellationToken = default);

    public Task EnsureNoBlockBetweenUsersAsync(long otherUserId, CancellationToken cancellationToken = default);

    public Task EnsureNoBlockBetweenUserAndOthersAsync(
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default);

    public Task EnsureGroupExistsAsync(long groupId, CancellationToken cancellationToken = default);

    public Task EnsureCallerIsGroupMemberAsync(long groupId, CancellationToken cancellationToken = default);

    public Task EnsureGroupMembersAsync(
        long groupId,
        IReadOnlyCollection<long> userIds,
        string membersErrorMessage,
        CancellationToken cancellationToken = default);

    public Task EnsureGroupMemberAsync(
        long groupId,
        long userId,
        string notFoundMessage,
        CancellationToken cancellationToken = default);
}
