using Api.Domain.Users.Repository;
using Api.Global.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.Domain.Users.Service;

[Service]
public class NicknameCacheService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly IUserRepository _repo;
    private readonly IDistributedCache _cache;

    public NicknameCacheService(IUserRepository repo, IDistributedCache cache)
    {
        _repo = repo;
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(IEnumerable<long> userIds)
    {
        var distinctUserIds = userIds.Distinct().ToList();
        if (distinctUserIds.Count == 0)
            return new Dictionary<long, string>();

        var result = new Dictionary<long, string>(distinctUserIds.Count);
        var missingUserIds = new List<long>();

        foreach (var userId in distinctUserIds)
        {
            var cachedNickname = await _cache.GetStringAsync(GetCacheKey(userId));
            if (string.IsNullOrWhiteSpace(cachedNickname))
            {
                missingUserIds.Add(userId);
                continue;
            }

            result[userId] = cachedNickname;
        }

        if (missingUserIds.Count > 0)
        {
            var nicknamesFromRepo = await _repo.GetNicknamesByIdsAsync(missingUserIds);
            foreach (var (userId, nickname) in nicknamesFromRepo)
            {
                result[userId] = nickname;
                await _cache.SetStringAsync(
                    GetCacheKey(userId),
                    nickname,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = CacheTtl,
                    });
            }
        }

        return result;
    }

    public Task InvalidateUserNicknameAsync(long userId) =>
        _cache.RemoveAsync(GetCacheKey(userId));

    private static string GetCacheKey(long userId) => $"users:nickname:{userId}";
}
