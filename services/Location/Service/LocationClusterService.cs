using System.Globalization;
using System.Text.Json;
using Location.Db;
using Location.Dto;
using Location.Repository;
using Location.Infrastructure;
using Location.Queue;
using Microsoft.Extensions.Caching.Distributed;
using Tangle.AspNetCore.Outbox;

namespace Location.Service;

[Service]
public class LocationClusterService(
    IMapPinRepository repo,
    IDistributedCache cache,
    IOutboxWriter outbox,
    LocationDbContext db)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ClusterCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EnqueueDebounceTtl = TimeSpan.FromSeconds(30);

    private readonly IMapPinRepository _repo = repo;
    private readonly IDistributedCache _cache = cache;
    private readonly IOutboxWriter _outbox = outbox;
    private readonly LocationDbContext _db = db;

    public async Task<List<MapClusterGetResponseDto>?> GetClustersAsync(MapClusterBoundsQueryDto query)
    {
        ValidateClusterBoundsQuery(query);

        var cacheKey = BuildCacheKey(query);
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached is not null)
        {
            return JsonSerializer.Deserialize<List<MapClusterGetResponseDto>>(cached, SerializerOptions);
        }

        await TryEnqueueClusterJobAsync(query);
        return null;
    }

    /// <summary>
    /// Worker path: return all pins in bounds for interim global cluster cache.
    /// Per-viewer visibility applies to pin list endpoints, not shared cluster tiles.
    /// </summary>
    public async Task<List<MapPinClusterPointDto>?> GetClusterPointsInBoundsAsync(MapPinBoundsQueryDto query)
    {
        ValidatePinBoundsQuery(query);

        var pins = await _repo.GetMapPinsInBoundsAsync(
            query.MinLatitude,
            query.MaxLatitude,
            query.MinLongitude,
            query.MaxLongitude);
        if (pins.Count == 0) return null;

        return [.. pins.Select(pin => new MapPinClusterPointDto(pin.Id, pin.Latitude, pin.Longitude))];
    }

    public async Task StoreClustersAsync(LocationClusterStoreRequestDto request)
    {
        ValidateClusterStoreRequest(request);
        var cacheKey = BuildCacheKey(request);
        var payload = JsonSerializer.Serialize(request.Clusters, SerializerOptions);
        await _cache.SetStringAsync(
            cacheKey,
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ClusterCacheTtl });
        await _cache.RemoveAsync(PendingKey(cacheKey));
    }

    public Task RefreshClustersNearPinAsync(decimal latitude, decimal longitude) =>
        RefreshClustersInBoundsAsync(ExpandBounds(latitude, longitude, paddingDegrees: 5m));

    public async Task RefreshClustersInBoundsAsync(MapPinBoundsQueryDto bounds)
    {
        ValidatePinBoundsQuery(bounds);
        for (var zoom = LocationClusterRules.MinZoom; zoom <= LocationClusterRules.MaxZoom; zoom++)
        {
            await TryEnqueueClusterJobAsync(new MapClusterBoundsQueryDto
            {
                MinLatitude = bounds.MinLatitude,
                MaxLatitude = bounds.MaxLatitude,
                MinLongitude = bounds.MinLongitude,
                MaxLongitude = bounds.MaxLongitude,
                Zoom = zoom,
            });
        }
    }

    private async Task TryEnqueueClusterJobAsync(MapClusterBoundsQueryDto query)
    {
        var debounceKey = PendingKey(BuildCacheKey(query));
        if (await _cache.GetStringAsync(debounceKey) is not null) return;

        await _cache.SetStringAsync(
            debounceKey,
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = EnqueueDebounceTtl });

        // Durable enqueue via transactional outbox (dispatcher XADDs to Redis Streams).
        _outbox.EnqueueWorkQueue(
            WorkQueueStreams.LocationCluster,
            new LocationClusterJob(
                query.MinLatitude,
                query.MaxLatitude,
                query.MinLongitude,
                query.MaxLongitude,
                query.Zoom));
        await _db.SaveChangesAsync();
    }

    private static string PendingKey(string cacheKey) => $"{cacheKey}:pending";

    internal static string BuildCacheKey(MapClusterBoundsQueryDto query) =>
        BuildCacheKey(
            query.MinLatitude,
            query.MaxLatitude,
            query.MinLongitude,
            query.MaxLongitude,
            query.Zoom);

    internal static string BuildCacheKey(LocationClusterStoreRequestDto request) =>
        BuildCacheKey(
            request.MinLatitude,
            request.MaxLatitude,
            request.MinLongitude,
            request.MaxLongitude,
            request.Zoom);

    private static string BuildCacheKey(
        decimal minLatitude,
        decimal maxLatitude,
        decimal minLongitude,
        decimal maxLongitude,
        int zoom) =>
        $"location:clusters:{Q(minLatitude)}:{Q(maxLatitude)}:{Q(minLongitude)}:{Q(maxLongitude)}:{zoom}";

    private static string Q(decimal value) => value.ToString("F4", CultureInfo.InvariantCulture);

    private static MapPinBoundsQueryDto ExpandBounds(decimal latitude, decimal longitude, decimal paddingDegrees) =>
        new()
        {
            MinLatitude = Math.Max(-90m, latitude - paddingDegrees),
            MaxLatitude = Math.Min(90m, latitude + paddingDegrees),
            MinLongitude = Math.Max(-180m, longitude - paddingDegrees),
            MaxLongitude = Math.Min(180m, longitude + paddingDegrees),
        };

    private static void ValidateClusterBoundsQuery(MapClusterBoundsQueryDto query)
    {
        ValidateLatLngBounds(query.MinLatitude, query.MaxLatitude, query.MinLongitude, query.MaxLongitude);
        if (query.Zoom is < LocationClusterRules.MinZoom or > LocationClusterRules.MaxZoom)
            throw new ArgumentException($"Zoom must be between {LocationClusterRules.MinZoom} and {LocationClusterRules.MaxZoom}.");
    }

    private static void ValidateClusterStoreRequest(LocationClusterStoreRequestDto request)
    {
        ValidateLatLngBounds(request.MinLatitude, request.MaxLatitude, request.MinLongitude, request.MaxLongitude);
        if (request.Zoom is < LocationClusterRules.MinZoom or > LocationClusterRules.MaxZoom)
            throw new ArgumentException($"Zoom must be between {LocationClusterRules.MinZoom} and {LocationClusterRules.MaxZoom}.");
    }

    private static void ValidatePinBoundsQuery(MapPinBoundsQueryDto query) =>
        ValidateLatLngBounds(query.MinLatitude, query.MaxLatitude, query.MinLongitude, query.MaxLongitude);

    private static void ValidateLatLngBounds(
        decimal minLatitude,
        decimal maxLatitude,
        decimal minLongitude,
        decimal maxLongitude)
    {
        if (minLatitude > maxLatitude)
            throw new ArgumentException("MinLatitude must be less than or equal to MaxLatitude.");

        if (minLongitude > maxLongitude)
            throw new ArgumentException("MinLongitude must be less than or equal to MaxLongitude.");
    }
}
