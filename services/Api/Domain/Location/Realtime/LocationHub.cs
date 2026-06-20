using Api.Domain.Location.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Domain.Location.Realtime;

[Authorize]
public class LocationHub(LocationSessionService sessionService) : Hub
{
    public const string LocationUpdatedEvent = "LocationUpdated";

    private readonly LocationSessionService _sessionService = sessionService;

    public static string SessionGroup(long sessionId) => $"session:{sessionId}";

    public async Task JoinSession(long sessionId)
    {
        await _sessionService.EnsureCanViewSessionAsync(sessionId, GetUserId());
        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }

    public Task LeaveSession(long sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));

    private long GetUserId() => long.Parse(Context.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));
}
