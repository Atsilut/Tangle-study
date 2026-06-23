using System.Text.Json.Serialization;

namespace Api.Domain.Location.Dto;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LocationSafetyAlertType
{
    StalePosition,
    Sos,
}

public record LocationSafetyAlertDto(
    LocationSafetyAlertType Type,
    long GroupId,
    long SessionId,
    long UserId,
    string UserNickname,
    decimal? Latitude,
    decimal? Longitude,
    DateTime OccurredAt,
    string Message);
