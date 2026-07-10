namespace Group.Client;

public interface ISocialClient
{
    public Task<bool> IsBlockedByAsync(
        long blockerUserId,
        long blockedUserId,
        CancellationToken cancellationToken = default);
}
