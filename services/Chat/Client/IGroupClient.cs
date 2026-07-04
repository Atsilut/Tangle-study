namespace Chat.Client;

public interface IGroupClient
{
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
