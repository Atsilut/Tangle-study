using System.Net.Http.Json;
using System.Text.Json;
using Users.Config;
using Microsoft.Extensions.Options;

namespace Users.Client;

internal sealed class HttpLocationClient(
    IHttpClientFactory httpClientFactory,
    IOptions<LocationClientOptions> options) : ILocationClient
{
    public const string InternalSecretHeaderName = "X-Internal-Secret";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly LocationClientOptions _options = options.Value;

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

        var client = _httpClientFactory.CreateClient(nameof(HttpLocationClient));
        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/location/posts/locations-by-ids")
        {
            Content = JsonContent.Create(new { postIds = ids }, options: SerializerOptions),
        };
        AddInternalSecret(request);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LocationPostLocationsResponse>(SerializerOptions, cancellationToken)
            ?? throw new InvalidOperationException("Location locations response was empty.");

        return payload.Locations.ToDictionary(
            entry => entry.PostId,
            entry => new PostLocationGetResponseDto(entry.Latitude, entry.Longitude));
    }

    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/location/users/{userId}/detach-on-deletion", cancellationToken);

    public Task EndSessionsForGroupAsync(long groupId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/location/groups/{groupId}/end-sessions", cancellationToken);

    private Task PostNoContentAsync(string relativePath, CancellationToken cancellationToken) =>
        PostNoContentAsync(relativePath, content: null, cancellationToken);

    private async Task PostNoContentAsync(
        string relativePath,
        object? content,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpLocationClient));
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);
        if (content is not null)
            request.Content = JsonContent.Create(content, options: SerializerOptions);
        AddInternalSecret(request);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private void AddInternalSecret(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation(InternalSecretHeaderName, _options.InternalSecret);
    }

    private sealed record LocationPostLocationsResponse(IReadOnlyList<LocationPostLocationEntry> Locations);

    private sealed record LocationPostLocationEntry(long PostId, decimal Latitude, decimal Longitude);
}
