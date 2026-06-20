namespace Api.Domain.Location.Dto;

public record MapPinGetResponseDto(
    long Id,
    decimal Latitude,
    decimal Longitude,
    long OwnerUserId,
    string OwnerNickname,
    long? PostId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
