namespace Community.Client;

public interface ISocialClient
{
    public Task<IReadOnlyCollection<long>> GetMutuallyBlockedUserIdsAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default);
}
