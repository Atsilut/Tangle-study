namespace Location.Client;

public interface IMonolithAccessClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default);

    public Task<HashSet<long>> GetMutuallyBlockedUserIdsAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default);

    public Task<bool> AnyBlockExistsBetweenUserAndOthersAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default);

    public Task EnsureGroupMemberAsync(
        long groupId,
        long userId,
        string notFoundMessage,
        CancellationToken cancellationToken = default);

    public Task<bool> IsGroupMemberAsync(long groupId, long userId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<GroupMemberSummaryDto>> GetGroupMembersForMemberAsync(
        long groupId,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<long>> GetGroupMemberUserIdsAsync(
        long groupId,
        CancellationToken cancellationToken = default);
}
