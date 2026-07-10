using System.Text.Json;
using Location.Config;
using Location.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Location.Storage;

public record LiveLocationSnapshot(
    long SessionId,
    long GroupId,
    long UserId,
    decimal Latitude,
    decimal Longitude,
    DateTime UpdatedAt);

[Service]
public class LiveLocationRedisStore(
    IDistributedCache cache,
    IServiceProvider serviceProvider,
    IOptions<RedisOptions> redisOptions,
    IOptions<LocationSafetyOptions> locationSafetyOptions)
{
    private const string KeyPrefix = "location:live:";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache = cache;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly RedisOptions _redisOptions = redisOptions.Value;
    private readonly LocationSafetyOptions _locationSafetyOptions = locationSafetyOptions.Value;

    private TimeSpan LiveLocationTtl =>
        TimeSpan.FromMinutes(_locationSafetyOptions.LivePositionTtlMinutes);

    public Task SetLiveLocationAsync(LiveLocationSnapshot snapshot)
    {
        var payload = Serialize(snapshot);
        if (_serviceProvider.GetService<IConnectionMultiplexer>() is { } multiplexer)
        {
            return multiplexer.GetDatabase().StringSetAsync(
                GetRedisKey(snapshot.GroupId, snapshot.UserId),
                payload,
                LiveLocationTtl);
        }

        return _cache.SetStringAsync(
            GetDistributedCacheKey(snapshot.GroupId, snapshot.UserId),
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = LiveLocationTtl });
    }

    public async Task<LiveLocationSnapshot?> GetLiveLocationAsync(long groupId, long userId)
    {
        var payload = await GetPayloadAsync(groupId, userId);
        return payload is null ? null : Deserialize(groupId, userId, payload);
    }

    public async Task<IReadOnlyDictionary<long, LiveLocationSnapshot>> GetLiveLocationsAsync(
        long groupId,
        IEnumerable<long> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<long, LiveLocationSnapshot>();

        if (_serviceProvider.GetService<IConnectionMultiplexer>() is { } multiplexer)
            return await GetLiveLocationsFromRedisAsync(multiplexer, groupId, ids);

        return await GetLiveLocationsFromDistributedCacheAsync(groupId, ids);
    }

    public Task RemoveLiveLocationAsync(long groupId, long userId)
    {
        if (_serviceProvider.GetService<IConnectionMultiplexer>() is { } multiplexer)
            return multiplexer.GetDatabase().KeyDeleteAsync(GetRedisKey(groupId, userId));

        return _cache.RemoveAsync(GetDistributedCacheKey(groupId, userId));
    }

    private async Task<string?> GetPayloadAsync(long groupId, long userId)
    {
        if (_serviceProvider.GetService<IConnectionMultiplexer>() is { } multiplexer)
        {
            var value = await multiplexer.GetDatabase().StringGetAsync(GetRedisKey(groupId, userId));
            return value.IsNullOrEmpty ? null : value.ToString();
        }

        return await _cache.GetStringAsync(GetDistributedCacheKey(groupId, userId));
    }

    private async Task<IReadOnlyDictionary<long, LiveLocationSnapshot>> GetLiveLocationsFromRedisAsync(
        IConnectionMultiplexer multiplexer,
        long groupId,
        IReadOnlyList<long> userIds)
    {
        var database = multiplexer.GetDatabase();
        var keys = userIds.Select(userId => (RedisKey)GetRedisKey(groupId, userId)).ToArray();
        var values = await database.StringGetAsync(keys);

        Dictionary<long, LiveLocationSnapshot> result = [];
        for (var i = 0; i < userIds.Count; i++)
        {
            if (values[i].IsNullOrEmpty) continue;
            var snapshot = Deserialize(groupId, userIds[i], values[i]!);
            if (snapshot is not null) result[userIds[i]] = snapshot;
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<long, LiveLocationSnapshot>> GetLiveLocationsFromDistributedCacheAsync(
        long groupId,
        IReadOnlyList<long> userIds)
    {
        var lookups = await Task.WhenAll(userIds.Select(async userId =>
        {
            var payload = await _cache.GetStringAsync(GetDistributedCacheKey(groupId, userId));
            return (userId, payload);
        }));

        Dictionary<long, LiveLocationSnapshot> result = [];
        foreach (var (userId, payload) in lookups)
        {
            if (string.IsNullOrWhiteSpace(payload)) continue;
            var snapshot = Deserialize(groupId, userId, payload);
            if (snapshot is not null) result[userId] = snapshot;
        }

        return result;
    }

    private static string Serialize(LiveLocationSnapshot snapshot) =>
        JsonSerializer.Serialize(
            new StoredLiveLocation(
                snapshot.SessionId,
                snapshot.GroupId,
                snapshot.Latitude,
                snapshot.Longitude,
                snapshot.UpdatedAt),
            SerializerOptions);

    private static LiveLocationSnapshot? Deserialize(long groupId, long userId, string payload)
    {
        var stored = JsonSerializer.Deserialize<StoredLiveLocation>(payload, SerializerOptions);
        if (stored is null) return null;

        return new LiveLocationSnapshot(
            stored.SessionId,
            stored.GroupId != 0 ? stored.GroupId : groupId,
            userId,
            stored.Latitude,
            stored.Longitude,
            stored.UpdatedAt);
    }

    private static string GetDistributedCacheKey(long groupId, long userId) => $"{KeyPrefix}{groupId}:{userId}";

    private string GetRedisKey(long groupId, long userId) =>
        $"{_redisOptions.InstanceName}{KeyPrefix}{groupId}:{userId}";

    private sealed record StoredLiveLocation(
        long SessionId,
        long GroupId,
        decimal Latitude,
        decimal Longitude,
        DateTime UpdatedAt);
}
