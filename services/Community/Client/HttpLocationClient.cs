using Community.Config;
using Community.Dto;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Community.Client;

internal sealed class HttpLocationClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<LocationClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpLocationClient)), ILocationClient
{
    public Task UpsertLocationForPostAsync(
        long postId,
        long userId,
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            $"internal/location/posts/{postId}/upsert",
            new { userId, latitude, longitude },
            cancellationToken);

    public Task ClearLocationForPostAsync(
        long postId,
        long userId,
        CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            $"internal/location/posts/{postId}/clear",
            new { userId },
            cancellationToken);

    public Task ClearLocationForPostOnDeleteAsync(long postId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/location/posts/{postId}/clear-on-delete", cancellationToken);

    public async Task<IReadOnlyDictionary<long, PostLocationGetResponseDto>> GetLocationsByPostIdsAsync(
        IReadOnlyCollection<long> postIds,
        CancellationToken cancellationToken = default)
    {
        var ids = postIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<long, PostLocationGetResponseDto>();

        var payload = await PostJsonAsync<LocationPostLocationsResponse>(
            "internal/location/posts/locations-by-ids",
            new { postIds = ids },
            cancellationToken)
            ?? throw new InvalidOperationException("Location locations response was empty.");

        return payload.Locations.ToDictionary(
            entry => entry.PostId,
            entry => new PostLocationGetResponseDto(entry.Latitude, entry.Longitude));
    }

    private sealed record LocationPostLocationsResponse(IReadOnlyList<LocationPostLocationEntry> Locations);

    private sealed record LocationPostLocationEntry(long PostId, decimal Latitude, decimal Longitude);
}
