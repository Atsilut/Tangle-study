namespace Chat.Client;

public interface ISocialClient
{
    public Task EnsureFriendshipExistsForUserPairAsync(
        long userId,
        long otherUserId,
        CancellationToken cancellationToken = default);

    public Task EnsureNoBlockBetweenUsersAsync(
        long userId,
        long otherUserId,
        CancellationToken cancellationToken = default);

    public Task EnsureNoBlockBetweenUserAndOthersAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default);
}
