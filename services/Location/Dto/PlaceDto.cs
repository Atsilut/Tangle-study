namespace Location.Dto;

public record PlaceSearchResultDto(
    string PlaceId,
    string DisplayName,
    decimal Latitude,
    decimal Longitude);

public record PlaceReverseResponseDto(string DisplayName);
