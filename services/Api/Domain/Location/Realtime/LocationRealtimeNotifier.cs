using Api.Domain.Location.Dto;
using Api.Global.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace Api.Domain.Location.Realtime;

[Service]
public class LocationRealtimeNotifier(IHubContext<LocationHub> hub) : ILocationRealtimeNotifier
{
    private readonly IHubContext<LocationHub> _hub = hub;

    public Task NotifyLocationUpdatedAsync(long sessionId, LiveLocationGetResponseDto location) =>
        _hub.Clients
            .Group(LocationHub.SessionGroup(sessionId))
            .SendAsync(LocationHub.LocationUpdatedEvent, location);
}
