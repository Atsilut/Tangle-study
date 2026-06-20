using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Service;
using Api.Domain.Location.Domain;
using Api.Domain.Location.Dto;
using Api.Domain.Location.Realtime;
using Api.Domain.Location.Repository;
using Api.Domain.Location.Storage;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Location.Service;

[Service]
public class LocationSessionService(
    ILocationSessionRepository repo,
    GroupMembershipService groupMembership,
    UserBlockService userBlockService,
    UserService userService,
    LiveLocationRedisStore liveStore,
    ILocationRealtimeNotifier realtime,
    LocationSafetyAlertService safetyAlerts,
    IHttpContextAccessor httpContextAccessor)
{
    private readonly ILocationSessionRepository _repo = repo;
    private readonly GroupMembershipService _groupMembership = groupMembership;
    private readonly UserBlockService _userBlockService = userBlockService;
    private readonly UserService _userService = userService;
    private readonly LiveLocationRedisStore _liveStore = liveStore;
    private readonly ILocationRealtimeNotifier _realtime = realtime;
    private readonly LocationSafetyAlertService _safetyAlerts = safetyAlerts;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public async Task<LocationSessionGetResponseDto> StartSessionAsync(LocationSessionCreateRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        await _userService.EnsureUserExistsAsync(userId, statusCode: StatusCodes.Status400BadRequest);
        ValidateCoordinates(request.Latitude, request.Longitude);
        await _groupMembership.EnsureMemberAsync(request.GroupId, userId, "Group not found");

        await EndActiveSessionForUserInGroupAsync(userId, request.GroupId);

        var session = new LocationSession(userId, request.GroupId);
        await _repo.CreateSessionAsync(session);

        var snapshot = await WriteLiveLocationAsync(session, request.Latitude, request.Longitude);
        return await MapSessionAsync(session, snapshot);
    }

    public async Task<LocationSessionGetResponseDto?> GetMyActiveSessionAsync(long groupId)
    {
        var userId = GetUserIdFromLogin();
        await _groupMembership.EnsureMemberAsync(groupId, userId, "Group not found");

        var session = await _repo.GetActiveSessionForUserInGroupAsync(userId, groupId);
        if (session is null) return null;

        var live = await _liveStore.GetLiveLocationAsync(groupId, userId);
        if (live is null || live.SessionId != session.Id)
        {
            await EndGhostSessionAsync(session);
            return null;
        }

        return await MapSessionAsync(session, live);
    }

    public async Task<List<LiveLocationGetResponseDto>?> GetActiveGroupLocationsAsync(long groupId)
    {
        var viewerId = GetUserIdFromLogin();
        await _groupMembership.EnsureMemberAsync(groupId, viewerId, "Group not found");

        var sessions = await _repo.GetActiveSessionsForGroupAsync(groupId);
        if (sessions.Count == 0) return null;

        var memberIds = sessions
            .Where(s => s.UserId is not null && s.UserId != viewerId)
            .Select(s => s.UserId!.Value)
            .Distinct()
            .ToList();
        if (memberIds.Count == 0) return null;

        var visibleMemberIds = new List<long>();
        foreach (var memberId in memberIds)
        {
            if (await _userBlockService.AnyBlockExistsBetweenUserAndOthersAsync(viewerId, [memberId])) continue;
            visibleMemberIds.Add(memberId);
        }

        if (visibleMemberIds.Count == 0) return null;

        var liveByUserId = await _liveStore.GetLiveLocationsAsync(groupId, visibleMemberIds);
        var nicknames = await _userService.GetNicknamesByUserIdsAsync(visibleMemberIds);

        List<LiveLocationGetResponseDto> result = [];
        foreach (var session in sessions)
        {
            if (session.UserId is null || session.UserId == viewerId) continue;
            if (!visibleMemberIds.Contains(session.UserId.Value)) continue;
            if (!liveByUserId.TryGetValue(session.UserId.Value, out var live)) continue;
            if (live.SessionId != session.Id) continue;

            result.Add(new LiveLocationGetResponseDto(
                session.Id,
                session.GroupId,
                session.OwnerUserId,
                nicknames.GetValueOrDefault(session.OwnerUserId, "Deleted User"),
                live.Latitude,
                live.Longitude,
                live.UpdatedAt));
        }

        return result.Count == 0 ? null : result;
    }

    public async Task<List<GroupMemberLocationStatusDto>> GetGroupMemberSharingStatusAsync(long groupId)
    {
        var viewerId = GetUserIdFromLogin();
        var members = await _groupMembership.GetMembersForMemberAsync(groupId, viewerId);
        var otherMembers = members.Where(m => m.UserId != viewerId).ToList();
        if (otherMembers.Count == 0) return [];

        var visibleMembers = new List<GroupMemberGetResponseDto>();
        foreach (var member in otherMembers)
        {
            if (await _userBlockService.AnyBlockExistsBetweenUserAndOthersAsync(viewerId, [member.UserId]))
                continue;
            visibleMembers.Add(member);
        }

        if (visibleMembers.Count == 0) return [];

        var sessions = await _repo.GetActiveSessionsForGroupAsync(groupId);
        var sessionByUserId = sessions
            .Where(s => s.UserId is not null)
            .GroupBy(s => s.UserId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.StartedAt).First());

        var sharingUserIds = sessionByUserId.Keys.ToList();
        var liveByUserId = sharingUserIds.Count == 0
            ? new Dictionary<long, LiveLocationSnapshot>()
            : await _liveStore.GetLiveLocationsAsync(groupId, sharingUserIds);

        List<GroupMemberLocationStatusDto> result = [];
        foreach (var member in visibleMembers)
        {
            LiveLocationSnapshot? live = null;
            LocationSession? session = null;
            if (sessionByUserId.TryGetValue(member.UserId, out session)
                && liveByUserId.TryGetValue(member.UserId, out var snapshot)
                && snapshot.SessionId == session.Id)
                live = snapshot;

            result.Add(new GroupMemberLocationStatusDto(
                member.UserId,
                member.Nickname,
                live is not null,
                live?.SessionId,
                live?.Latitude,
                live?.Longitude,
                live?.UpdatedAt));
        }

        return result
            .OrderByDescending(m => m.IsSharing)
            .ThenBy(m => m.UserNickname, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<LocationSessionGetResponseDto> UpdatePositionAsync(
        long sessionId,
        LocationPositionUpdateRequestDto request)
    {
        var userId = GetUserIdFromLogin();
        var session = await GetActiveSessionOwnedByUserOrThrowAsync(sessionId, userId);
        ValidateCoordinates(request.Latitude, request.Longitude);

        var snapshot = await WriteLiveLocationAsync(session, request.Latitude, request.Longitude);
        var dto = new LiveLocationGetResponseDto(
            session.Id,
            session.GroupId,
            session.OwnerUserId,
            (await _userService.GetNicknamesByUserIdsAsync([session.OwnerUserId]))
                .GetValueOrDefault(session.OwnerUserId, "Deleted User"),
            snapshot.Latitude,
            snapshot.Longitude,
            snapshot.UpdatedAt);

        await _realtime.NotifyLocationUpdatedAsync(session.Id, dto);
        await _safetyAlerts.ClearStaleAlertStateAsync(session.Id);
        return await MapSessionAsync(session, snapshot);
    }

    public async Task StopSessionAsync(long sessionId)
    {
        var userId = GetUserIdFromLogin();
        var session = await GetActiveSessionOwnedByUserOrThrowAsync(sessionId, userId);
        session.End();
        await _repo.UpdateSessionAsync(session);
        await _liveStore.RemoveLiveLocationAsync(session.GroupId, userId);
    }

    public async Task EnsureCanViewSessionAsync(long sessionId, long viewerUserId)
    {
        var session = await _repo.GetSessionByIdAsync(sessionId)
            ?? throw new EntityNotFoundException("Location session not found");

        if (!session.IsActive) throw new ArgumentException("Location session is not active.");

        if (session.OwnerUserId == viewerUserId) return;

        if (await _userBlockService.AnyBlockExistsBetweenUserAndOthersAsync(viewerUserId, [session.OwnerUserId]))
            throw new UnauthorizedAccessException("Unauthorized access");

        if (!await _groupMembership.IsMemberAsync(session.GroupId, viewerUserId))
            throw new UnauthorizedAccessException("You must be a group member to view this location session.");
    }

    public async Task HandleUserDeletionAsync(long userId)
    {
        var sessions = (await _repo.GetAllActiveSessionsAsync())
            .Where(s => s.UserId == userId)
            .ToList();

        await _repo.EndAllActiveSessionsForUserAsync(userId);

        foreach (var session in sessions)
            await _liveStore.RemoveLiveLocationAsync(session.GroupId, userId);
    }

    public async Task ReconcileGhostSessionsAsync()
    {
        var sessions = await _repo.GetAllActiveSessionsAsync();
        foreach (var session in sessions)
        {
            if (session.UserId is null) continue;

            var live = await _liveStore.GetLiveLocationAsync(session.GroupId, session.UserId.Value);
            if (live is not null && live.SessionId == session.Id) continue;

            await EndGhostSessionAsync(session);
        }
    }

    private async Task EndActiveSessionForUserInGroupAsync(long userId, long groupId)
    {
        var active = await _repo.GetActiveSessionForUserInGroupAsync(userId, groupId);
        if (active is null) return;

        active.End();
        await _repo.UpdateSessionAsync(active);
        await _liveStore.RemoveLiveLocationAsync(groupId, userId);
    }

    private async Task<LocationSession> GetActiveSessionOwnedByUserOrThrowAsync(long sessionId, long userId)
    {
        var session = await _repo.GetSessionByIdAsync(sessionId)
            ?? throw new EntityNotFoundException("Location session not found");

        if (!session.IsActive) throw new ArgumentException("Location session is not active.");
        if (session.OwnerUserId != userId) throw new UnauthorizedAccessException("Unauthorized access");

        return session;
    }

    private async Task EndGhostSessionAsync(LocationSession session)
    {
        if (!session.IsActive || session.UserId is null) return;

        session.End();
        await _repo.UpdateSessionAsync(session);
        await _liveStore.RemoveLiveLocationAsync(session.GroupId, session.UserId.Value);
        await _safetyAlerts.ClearStaleAlertStateAsync(session.Id);
    }

    private async Task<LiveLocationSnapshot> WriteLiveLocationAsync(
        LocationSession session,
        decimal latitude,
        decimal longitude)
    {
        var snapshot = new LiveLocationSnapshot(
            session.Id,
            session.GroupId,
            session.OwnerUserId,
            latitude,
            longitude,
            DateTime.UtcNow);
        await _liveStore.SetLiveLocationAsync(snapshot);
        return snapshot;
    }

    private async Task<LocationSessionGetResponseDto> MapSessionAsync(
        LocationSession session,
        LiveLocationSnapshot snapshot)
    {
        var nickname = (await _userService.GetNicknamesByUserIdsAsync([session.OwnerUserId]))
            .GetValueOrDefault(session.OwnerUserId, "Deleted User");

        return new LocationSessionGetResponseDto(
            session.Id,
            session.GroupId,
            session.OwnerUserId,
            nickname,
            snapshot.Latitude,
            snapshot.Longitude,
            session.StartedAt,
            snapshot.UpdatedAt);
    }

    private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));

    private static void ValidateCoordinates(decimal latitude, decimal longitude)
    {
        if (latitude is < -90 or > 90) throw new ArgumentException("Latitude must be between -90 and 90.");
        if (longitude is < -180 or > 180) throw new ArgumentException("Longitude must be between -180 and 180.");
    }
}
