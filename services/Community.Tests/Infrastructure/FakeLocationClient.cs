using Community.Client;
using Community.Dto;

namespace Community.Tests.Infrastructure;

public sealed class FakeLocationClient : ILocationClient
{
    private readonly Dictionary<long, PostLocationGetResponseDto> _locations = [];
    private Exception? _upsertFailure;
    private Exception? _clearFailure;
    private Exception? _clearOnDeleteFailure;

    public void Reset()
    {
        _locations.Clear();
        _upsertFailure = null;
        _clearFailure = null;
        _clearOnDeleteFailure = null;
    }

    public void FailNextUpsert(Exception exception) => _upsertFailure = exception;

    public void FailNextClear(Exception exception) => _clearFailure = exception;

    public void FailNextClearOnDelete(Exception exception) => _clearOnDeleteFailure = exception;

    public bool HasLocation(long postId) => _locations.ContainsKey(postId);

    public Task UpsertLocationForPostAsync(
        long postId,
        long userId,
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default)
    {
        if (_upsertFailure is not null)
        {
            var failure = _upsertFailure;
            _upsertFailure = null;
            throw failure;
        }

        _locations[postId] = new PostLocationGetResponseDto(latitude, longitude);
        return Task.CompletedTask;
    }

    public Task ClearLocationForPostAsync(
        long postId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        if (_clearFailure is not null)
        {
            var failure = _clearFailure;
            _clearFailure = null;
            throw failure;
        }

        _locations.Remove(postId);
        return Task.CompletedTask;
    }

    public Task ClearLocationForPostOnDeleteAsync(long postId, CancellationToken cancellationToken = default)
    {
        if (_clearOnDeleteFailure is not null)
        {
            var failure = _clearOnDeleteFailure;
            _clearOnDeleteFailure = null;
            throw failure;
        }

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
