using Location.Dto;
using Location.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace Location.Realtime;

[Service]
public class LocationRealtimeNotifier(IHubContext<LocationHub> hub) : ILocationRealtimeNotifier
{
    private readonly IHubContext<LocationHub> _hub = hub;

    public Task NotifyLocationUpdatedAsync(long sessionId, LiveLocationGetResponseDto location) =>
        _hub.Clients
            .Group(LocationHub.SessionGroup(sessionId))
            .SendAsync(LocationHub.LocationUpdatedEvent, location);

    public Task NotifyLocationSessionEndedAsync(long sessionId, LocationSessionEndedDto ended) =>
        _hub.Clients
            .Group(LocationHub.SessionGroup(sessionId))
            .SendAsync(LocationHub.LocationSessionEndedEvent, ended);

    public Task NotifySafetyAlertAsync(LocationSafetyAlertDto alert, IReadOnlyList<long> recipientUserIds)
    {
        if (recipientUserIds.Count == 0) return Task.CompletedTask;

        var userIds = recipientUserIds.Select(id => id.ToString()).ToList();
        return _hub.Clients
            .Users(userIds)
            .SendAsync(LocationHub.SafetyAlertRaisedEvent, alert);
    }
}
