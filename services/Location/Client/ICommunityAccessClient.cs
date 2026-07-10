namespace Location.Client;

public interface ICommunityAccessClient
{
    public Task EnsurePostOwnerAsync(long postId, CancellationToken cancellationToken = default);

    public Task<HashSet<long>> GetViewablePostIdsAsync(
        IReadOnlyCollection<long> postIds,
        long? viewerUserId,
        CancellationToken cancellationToken = default);
}
