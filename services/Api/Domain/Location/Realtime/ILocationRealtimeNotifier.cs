using Api.Domain.Location.Dto;

namespace Api.Domain.Location.Realtime;

public interface ILocationRealtimeNotifier
{
    Task NotifyLocationUpdatedAsync(long sessionId, LiveLocationGetResponseDto location);
    Task NotifyLocationSessionEndedAsync(long sessionId, LocationSessionEndedDto ended);
    Task NotifySafetyAlertAsync(LocationSafetyAlertDto alert, IReadOnlyList<long> recipientUserIds);
}
