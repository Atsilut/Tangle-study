namespace Location.Client;

public interface IGroupClient
{
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
