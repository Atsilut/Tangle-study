using Api.Domain.Users.Repository;
using Api.Global.Config;
using Api.Global.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.Domain.Users.Service;

[Service]
public class NicknameCacheService(
    IUserRepository repo,
    IDistributedCache cache,
    IServiceProvider serviceProvider,
    IOptions<RedisOptions> redisOptions)
{
    private const string CacheKeyPrefix = "users:nickname:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly IUserRepository _repo = repo;
    private readonly IDistributedCache _cache = cache;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly RedisOptions _redisOptions = redisOptions.Value;

    public async Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(IEnumerable<long> userIds)
    {
        List<long> distinctUserIds = [.. userIds.Distinct()];
        if (distinctUserIds.Count == 0)
        {
            Dictionary<long, string> empty = [];
            return empty;
        }

        var result = new Dictionary<long, string>(distinctUserIds.Count);
        var cachedNicknames = await GetCachedNicknamesAsync(distinctUserIds);
        List<long> missingUserIds = [];

        foreach (var userId in distinctUserIds)
        {
            if (cachedNicknames.TryGetValue(userId, out var nickname))
            {
                result[userId] = nickname;
                continue;
            }

            missingUserIds.Add(userId);
        }

        if (missingUserIds.Count > 0)
        {
            var nicknamesFromRepo = await _repo.GetNicknamesByIdsAsync(missingUserIds);
            await SetCachedNicknamesAsync(nicknamesFromRepo);
            foreach (var (userId, nickname) in nicknamesFromRepo)
                result[userId] = nickname;
        }

        return result;
    }

    public async Task InvalidateUserNicknameAsync(long userId)
    {
        if (_serviceProvider.GetService<IConnectionMultiplexer>() is { } multiplexer)
        {
            await multiplexer.GetDatabase().KeyDeleteAsync(GetRedisKey(userId));
            return;
        }

        await _cache.RemoveAsync(GetDistributedCacheKey(userId));
    }

    private async Task<IReadOnlyDictionary<long, string>> GetCachedNicknamesAsync(IReadOnlyList<long> userIds)
    {
        if (_serviceProvider.GetService<IConnectionMultiplexer>() is { } multiplexer)
            return await GetCachedNicknamesFromRedisAsync(multiplexer, userIds);

        return await GetCachedNicknamesFromDistributedCacheAsync(userIds);
    }

    private async Task<IReadOnlyDictionary<long, string>> GetCachedNicknamesFromRedisAsync(
        IConnectionMultiplexer multiplexer,
        IReadOnlyList<long> userIds)
    {
        var database = multiplexer.GetDatabase();
        var keys = userIds.Select(userId => (RedisKey)GetRedisKey(userId)).ToArray();
        var values = await database.StringGetAsync(keys);

        Dictionary<long, string> result = [];
        for (var i = 0; i < userIds.Count; i++)
        {
            if (!values[i].IsNullOrEmpty) result[userIds[i]] = values[i]!;
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<long, string>> GetCachedNicknamesFromDistributedCacheAsync(
        IReadOnlyList<long> userIds)
    {
        var lookups = await Task.WhenAll(userIds.Select(async userId =>
        {
            var cachedNickname = await _cache.GetStringAsync(GetDistributedCacheKey(userId));
            return (userId, cachedNickname);
        }));

        Dictionary<long, string> result = [];
        foreach (var (userId, cachedNickname) in lookups)
        {
            if (!string.IsNullOrWhiteSpace(cachedNickname)) result[userId] = cachedNickname;
        }

        return result;
    }

    private async Task SetCachedNicknamesAsync(IReadOnlyDictionary<long, string> nicknames)
    {
        if (nicknames.Count == 0) return;

        if (_serviceProvider.GetService<IConnectionMultiplexer>() is { } multiplexer)
        {
            await SetCachedNicknamesInRedisAsync(multiplexer, nicknames);
            return;
        }

        await SetCachedNicknamesInDistributedCacheAsync(nicknames);
    }

    private async Task SetCachedNicknamesInRedisAsync(
        IConnectionMultiplexer multiplexer,
        IReadOnlyDictionary<long, string> nicknames)
    {
        var database = multiplexer.GetDatabase();
        var batch = database.CreateBatch();
        List<Task> writes = [];

        foreach (var (userId, nickname) in nicknames)
            writes.Add(batch.StringSetAsync(GetRedisKey(userId), nickname, CacheTtl));

        batch.Execute();
        await Task.WhenAll(writes);
    }

    private Task SetCachedNicknamesInDistributedCacheAsync(IReadOnlyDictionary<long, string> nicknames) =>
        Task.WhenAll(nicknames.Select(pair => _cache.SetStringAsync(
            GetDistributedCacheKey(pair.Key),
            pair.Value,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
            })));

    private static string GetDistributedCacheKey(long userId) => $"{CacheKeyPrefix}{userId}";

    private string GetRedisKey(long userId) => $"{_redisOptions.InstanceName}{CacheKeyPrefix}{userId}";
}
