using Location.Client;
using Location.Config;
using Location.Entities;
using Location.Dto;
using Location.Realtime;
using Location.Repository;
using Location.Storage;
using Location.Exceptions;
using Location.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Location.Service;

[Service]
public class LocationSafetyAlertService(
    ILocationSessionRepository repo,
    LiveLocationRedisStore liveStore,
    IMonolithAccessClient monolithAccess,
    ILocationRealtimeNotifier realtime,
    IDistributedCache cache,
    IOptions<LocationSafetyOptions> options,
    IHttpContextAccessor httpContextAccessor)
{
    private const string StaleAlertSentKeyPrefix = "location:safety:stale:";
    private const string SosCooldownKeyPrefix = "location:safety:sos:";

    private readonly ILocationSessionRepository _repo = repo;
    private readonly LiveLocationRedisStore _liveStore = liveStore;
    private readonly IMonolithAccessClient _monolithAccess = monolithAccess;
    private readonly ILocationRealtimeNotifier _realtime = realtime;
    private readonly IDistributedCache _cache = cache;
    private readonly LocationSafetyOptions _options = options.Value;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public Task EnsureCanJoinGroupAlertsAsync(long groupId, long userId) =>
        _monolithAccess.EnsureGroupMemberAsync(groupId, userId, "Group not found");

    public async Task<LocationSafetyAlertDto> TriggerSosAsync(long sessionId)
    {
        var userId = GetUserIdFromLogin();
        var session = await GetActiveSessionOwnedByUserOrThrowAsync(sessionId, userId);
        await EnsureSosCooldownAllowsAsync(session.Id);

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

        var recipients = await GetAlertRecipientUserIdsAsync(session.GroupId, session.OwnerUserId);
        await _realtime.NotifySafetyAlertAsync(alert, recipients);
        await MarkSosSentAsync(session.Id);
        return alert;
    }

    public async Task EvaluateStaleSessionsAsync()
    {
        var sessions = await _repo.GetAllActiveSessionsAsync();
        if (sessions.Count == 0) return;

        var threshold = TimeSpan.FromMinutes(_options.StalePositionMinutes);
        var now = DateTime.UtcNow;
        List<(LocationSession Session, LiveLocationSnapshot Live)> staleSessions = [];

        foreach (var session in sessions)
        {
            if (session.UserId is null) continue;

            var live = await _liveStore.GetLiveLocationAsync(session.GroupId, session.UserId.Value);
            if (live is null || live.SessionId != session.Id) continue;
            if (now - live.UpdatedAt < threshold) continue;
            if (await WasStaleAlertSentAsync(session.Id)) continue;

            staleSessions.Add((session, live));
        }

        if (staleSessions.Count == 0) return;

        var nicknames = await _monolithAccess.GetNicknamesByUserIdsAsync(
            staleSessions.Select(s => s.Session.OwnerUserId).Distinct());

        foreach (var (session, live) in staleSessions)
        {
            var nickname = nicknames.GetValueOrDefault(session.OwnerUserId, "Deleted User");
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

            var recipients = await GetAlertRecipientUserIdsAsync(session.GroupId, session.OwnerUserId);
            await _realtime.NotifySafetyAlertAsync(alert, recipients);
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

    private static string GetSosCooldownKey(long sessionId) => $"{SosCooldownKeyPrefix}{sessionId}";

    private async Task EnsureSosCooldownAllowsAsync(long sessionId)
    {
        if (_options.SosCooldownSeconds <= 0) return;

        var value = await _cache.GetStringAsync(GetSosCooldownKey(sessionId));
        if (!string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Please wait before sending another SOS alert.");
    }

    private Task MarkSosSentAsync(long sessionId) =>
        _cache.SetStringAsync(
            GetSosCooldownKey(sessionId),
            "1",
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.SosCooldownSeconds),
            });

    private async Task<List<long>> GetAlertRecipientUserIdsAsync(long groupId, long subjectUserId)
    {
        var memberIds = await _monolithAccess.GetGroupMemberUserIdsAsync(groupId);
        var candidateIds = memberIds.Where(id => id != subjectUserId).ToList();
        if (candidateIds.Count == 0) return [];

        var blockedWithSubject = await _monolithAccess.GetMutuallyBlockedUserIdsAsync(subjectUserId, candidateIds);
        return candidateIds.Where(id => !blockedWithSubject.Contains(id)).ToList();
    }

    private async Task<string> GetNicknameAsync(long userId) =>
        (await _monolithAccess.GetNicknamesByUserIdsAsync([userId]))
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
