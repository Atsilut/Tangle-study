using Api.Domain.Location.Dto;

namespace Api.Domain.Location.Realtime;

public interface ILocationRealtimeNotifier
{
    Task NotifyLocationUpdatedAsync(long sessionId, LiveLocationGetResponseDto location);
}
