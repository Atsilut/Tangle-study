using Community.Client;
using Community.Dto;

namespace Community.Tests.Infrastructure;

public sealed class FakeLocationClient : ILocationClient
{
    private readonly Dictionary<long, PostLocationGetResponseDto> _locations = [];

    public Task UpsertLocationForPostAsync(
        long postId,
        long userId,
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default)
    {
        _locations[postId] = new PostLocationGetResponseDto(latitude, longitude);
        return Task.CompletedTask;
    }

    public Task ClearLocationForPostAsync(
        long postId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        _locations.Remove(postId);
        return Task.CompletedTask;
    }

    public Task ClearLocationForPostOnDeleteAsync(long postId, CancellationToken cancellationToken = default)
    {
        _locations.Remove(postId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<long, PostLocationGetResponseDto>> GetLocationsByPostIdsAsync(
        IReadOnlyCollection<long> postIds,
        CancellationToken cancellationToken = default)
    {
        Dictionary<long, PostLocationGetResponseDto> result = [];
        foreach (var postId in postIds)
        {
            if (_locations.TryGetValue(postId, out var location))
                result[postId] = location;
        }

        return Task.FromResult<IReadOnlyDictionary<long, PostLocationGetResponseDto>>(result);
    }
}
