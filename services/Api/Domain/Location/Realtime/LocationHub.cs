using Api.Domain.Groups.Service;
using Api.Domain.Location.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Domain.Location.Realtime;

[Authorize]
public class LocationHub(
    LocationSessionService sessionService,
    GroupMembershipService groupMembership) : Hub
{
    public const string LocationUpdatedEvent = "LocationUpdated";
    public const string LocationSessionEndedEvent = "LocationSessionEnded";
    public const string SafetyAlertRaisedEvent = "SafetyAlertRaised";

    private readonly LocationSessionService _sessionService = sessionService;
    private readonly GroupMembershipService _groupMembership = groupMembership;

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
        await _groupMembership.EnsureMemberAsync(groupId, GetUserId(), "Group not found");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupAlertsGroup(groupId));
    }

    public Task LeaveGroupAlerts(long groupId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupAlertsGroup(groupId));

    private long GetUserId() => long.Parse(Context.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));
}
