namespace Chat.Client;

public interface ISocialClient
{
    public Task EnsureFriendshipExistsForUserPairAsync(long otherUserId, CancellationToken cancellationToken = default);

    public Task EnsureNoBlockBetweenUsersAsync(long otherUserId, CancellationToken cancellationToken = default);

    public Task EnsureNoBlockBetweenUserAndOthersAsync(
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default);
}
