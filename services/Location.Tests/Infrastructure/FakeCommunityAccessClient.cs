using Location.Client;
using Microsoft.AspNetCore.Http;
using Tangle.AspNetCore.Exceptions;

namespace Location.Tests.Infrastructure;

public sealed class FakeCommunityAccessClient(IHttpContextAccessor httpContextAccessor) : ICommunityAccessClient
{
    private long _nextPostId = 1;

    public Dictionary<long, long> PostOwners { get; } = [];
    public HashSet<(long PostId, long ViewerUserId)> ViewablePosts { get; } = [];

    public long SeedPost(long ownerUserId, long? postId = null)
    {
        var id = postId ?? _nextPostId++;
        PostOwners[id] = ownerUserId;
        return id;
    }

    public void SetPostViewable(long postId, long viewerUserId) =>
        ViewablePosts.Add((postId, viewerUserId));

    public void Reset()
    {
        PostOwners.Clear();
        ViewablePosts.Clear();
        _nextPostId = 1;
    }

    public Task EnsurePostOwnerAsync(long postId, CancellationToken cancellationToken = default)
    {
        if (!PostOwners.TryGetValue(postId, out var ownerId))
            throw new EntityNotFoundException("Post not found");

        var callerId = GetCallerUserId();
        if (callerId != ownerId)
            throw new UnauthorizedAccessException("Unauthorized access");

        return Task.CompletedTask;
    }

    public Task<bool> PostExistsAsync(long postId, CancellationToken cancellationToken = default) =>
        Task.FromResult(PostOwners.ContainsKey(postId));

    public Task<HashSet<long>> GetViewablePostIdsAsync(
        IReadOnlyCollection<long> postIds,
        long? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var viewable = new HashSet<long>();
        foreach (var postId in postIds)
        {
            if (!PostOwners.ContainsKey(postId)) continue;

            if (viewerUserId is null)
            {
                viewable.Add(postId);
                continue;
            }

            if (PostOwners[postId] == viewerUserId || ViewablePosts.Contains((postId, viewerUserId.Value)))
                viewable.Add(postId);
        }

        return Task.FromResult(viewable);
    }

    private long GetCallerUserId()
    {
        var sub = httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub))
            throw new UnauthorizedAccessException("Unauthorized access");
        return long.Parse(sub);
    }
}
