namespace Community.Service;

public static class MutualBlockFilter
{
    public static async Task<List<T>> FilterByMutualBlockAsync<T>(
        long? viewerUserId,
        IReadOnlyList<T> items,
        Func<T, long> authorIdSelector,
        Func<long, IReadOnlyCollection<long>, CancellationToken, Task<IReadOnlyCollection<long>>> getMutuallyBlockedUserIdsAsync,
        CancellationToken cancellationToken = default)
    {
        if (viewerUserId is null || items.Count == 0) return [.. items];

        var blockedAuthorIds = await getMutuallyBlockedUserIdsAsync(
            viewerUserId.Value,
            [.. items.Select(authorIdSelector).Distinct()],
            cancellationToken);
        if (blockedAuthorIds.Count == 0) return [.. items];

        return [.. items.Where(item => !blockedAuthorIds.Contains(authorIdSelector(item)))];
    }

    public static async Task<bool> IsAuthorBlockedByViewerAsync(
        long? viewerUserId,
        long authorUserId,
        Func<long, IReadOnlyCollection<long>, CancellationToken, Task<IReadOnlyCollection<long>>> getMutuallyBlockedUserIdsAsync,
        CancellationToken cancellationToken = default)
    {
        if (viewerUserId is null || viewerUserId.Value == authorUserId) return false;

        var blockedIds = await getMutuallyBlockedUserIdsAsync(viewerUserId.Value, [authorUserId], cancellationToken);
        return blockedIds.Contains(authorUserId);
    }
}
