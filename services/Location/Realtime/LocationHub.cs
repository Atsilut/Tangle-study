using Location.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Location.Realtime;

[Authorize]
public class LocationHub(
    LocationSessionService sessionService,
    LocationSafetyAlertService safetyAlerts) : Hub
{
    public const string LocationUpdatedEvent = "LocationUpdated";
    public const string LocationSessionEndedEvent = "LocationSessionEnded";
    public const string SafetyAlertRaisedEvent = "SafetyAlertRaised";

    private readonly LocationSessionService _sessionService = sessionService;
    private readonly LocationSafetyAlertService _safetyAlerts = safetyAlerts;

    public static string SessionGroup(long sessionId) => $"session:{sessionId}";
    public static string GroupAlertsGroup(long groupId) => $"group-alerts:{groupId}";

    public async Task JoinSession(long sessionId)
    {
        await _sessionService.EnsureCanViewSessionAsync(sessionId, GetUserId());
        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }

    public Task LeaveSession(long sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));

    public async Task JoinGroupAlerts(long groupId)
    {
        await _safetyAlerts.EnsureCanJoinGroupAlertsAsync(groupId, GetUserId());
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupAlertsGroup(groupId));
    }

    public Task LeaveGroupAlerts(long groupId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupAlertsGroup(groupId));

    private long GetUserId() => long.Parse(Context.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));
}
