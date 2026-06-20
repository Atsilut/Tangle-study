using Api.Domain.Location.Config;
using Api.Domain.Location.Domain;
using Api.Domain.Location.Dto;
using Api.Domain.Location.Realtime;
using Api.Domain.Location.Repository;
using Api.Domain.Location.Storage;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Api.Domain.Location.Service;

[Service]
public class LocationSafetyAlertService(
    ILocationSessionRepository repo,
    LiveLocationRedisStore liveStore,
    UserService userService,
    ILocationRealtimeNotifier realtime,
    IDistributedCache cache,
    IOptions<LocationSafetyOptions> options,
    IHttpContextAccessor httpContextAccessor)
{
    private const string StaleAlertSentKeyPrefix = "location:safety:stale:";

    private readonly ILocationSessionRepository _repo = repo;
    private readonly LiveLocationRedisStore _liveStore = liveStore;
    private readonly UserService _userService = userService;
    private readonly ILocationRealtimeNotifier _realtime = realtime;
    private readonly IDistributedCache _cache = cache;
    private readonly LocationSafetyOptions _options = options.Value;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public async Task<LocationSafetyAlertDto> TriggerSosAsync(long sessionId)
    {
        var userId = GetUserIdFromLogin();
        var session = await GetActiveSessionOwnedByUserOrThrowAsync(sessionId, userId);
        var live = await _liveStore.GetLiveLocationAsync(session.GroupId, userId);
        var nickname = await GetNicknameAsync(session.OwnerUserId);

        var alert = new LocationSafetyAlertDto(
            LocationSafetyAlertType.Sos,
            session.GroupId,
            session.Id,
            session.OwnerUserId,
            nickname,
            live?.Latitude,
            live?.Longitude,
            DateTime.UtcNow,
            $"{nickname} requested help via live location.");

        await _realtime.NotifySafetyAlertAsync(alert);
        return alert;
    }

    public async Task EvaluateStaleSessionsAsync()
    {
        var sessions = await _repo.GetAllActiveSessionsAsync();
        if (sessions.Count == 0) return;

        var threshold = TimeSpan.FromMinutes(_options.StalePositionMinutes);
        var now = DateTime.UtcNow;

        foreach (var session in sessions)
        {
            if (session.UserId is null) continue;

            var live = await _liveStore.GetLiveLocationAsync(session.GroupId, session.UserId.Value);
            if (live is null || live.SessionId != session.Id) continue;
            if (now - live.UpdatedAt < threshold) continue;
            if (await WasStaleAlertSentAsync(session.Id)) continue;

            var nickname = await GetNicknameAsync(session.OwnerUserId);
            var alert = new LocationSafetyAlertDto(
                LocationSafetyAlertType.StalePosition,
                session.GroupId,
                session.Id,
                session.OwnerUserId,
                nickname,
                live.Latitude,
                live.Longitude,
                now,
                $"{nickname}'s live location has not updated in {_options.StalePositionMinutes} minutes.");

            await _realtime.NotifySafetyAlertAsync(alert);
            await MarkStaleAlertSentAsync(session.Id);
        }
    }

    public Task ClearStaleAlertStateAsync(long sessionId) =>
        _cache.RemoveAsync(GetStaleAlertSentKey(sessionId));

    private async Task<bool> WasStaleAlertSentAsync(long sessionId)
    {
        var value = await _cache.GetStringAsync(GetStaleAlertSentKey(sessionId));
        return !string.IsNullOrWhiteSpace(value);
    }

    private Task MarkStaleAlertSentAsync(long sessionId) =>
        _cache.SetStringAsync(
            GetStaleAlertSentKey(sessionId),
            "1",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12),
            });

    private static string GetStaleAlertSentKey(long sessionId) => $"{StaleAlertSentKeyPrefix}{sessionId}";

    private async Task<string> GetNicknameAsync(long userId) =>
        (await _userService.GetNicknamesByUserIdsAsync([userId]))
            .GetValueOrDefault(userId, "Deleted User");

    private async Task<LocationSession> GetActiveSessionOwnedByUserOrThrowAsync(long sessionId, long userId)
    {
        var session = await _repo.GetSessionByIdAsync(sessionId)
            ?? throw new EntityNotFoundException("Location session not found");

        if (!session.IsActive) throw new ArgumentException("Location session is not active.");
        if (session.OwnerUserId != userId) throw new UnauthorizedAccessException("Unauthorized access");

        return session;
    }

    private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));
}
