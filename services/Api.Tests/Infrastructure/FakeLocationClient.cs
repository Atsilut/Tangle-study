using Api.Client;

namespace Api.Tests.Infrastructure;

public sealed class FakeLocationClient : ILocationClient
{
    private readonly Dictionary<long, PostLocationGetResponseDto> _locationsByPostId = new();
    public List<long> DetachedUserIds { get; } = [];
    public List<long> EndedSessionGroupIds { get; } = [];

    public Task UpsertLocationForPostAsync(
        long postId,
        long userId,
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default)
    {
        _locationsByPostId[postId] = new PostLocationGetResponseDto(latitude, longitude);
        return Task.CompletedTask;
    }

    public Task ClearLocationForPostAsync(
        long postId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        _locationsByPostId.Remove(postId);
        return Task.CompletedTask;
    }

    public Task ClearLocationForPostOnDeleteAsync(long postId, CancellationToken cancellationToken = default)
    {
        _locationsByPostId.Remove(postId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<long, PostLocationGetResponseDto>> GetLocationsByPostIdsAsync(
        IReadOnlyCollection<long> postIds,
        CancellationToken cancellationToken = default)
    {
        var result = postIds
            .Where(_locationsByPostId.ContainsKey)
            .ToDictionary(id => id, id => _locationsByPostId[id]);
        return Task.FromResult<IReadOnlyDictionary<long, PostLocationGetResponseDto>>(result);
    }

    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default)
    {
        DetachedUserIds.Add(userId);
        return Task.CompletedTask;
    }

    public Task EndSessionsForGroupAsync(long groupId, CancellationToken cancellationToken = default)
    {
        EndedSessionGroupIds.Add(groupId);
        return Task.CompletedTask;
    }
}
