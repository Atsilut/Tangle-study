using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Location.Dto;

public record MapClusterGetResponseDto(
    decimal Latitude,
    decimal Longitude,
    int PinCount,
    long? SamplePinId);

public record MapClusterBoundsQueryDto
{
    [Required]
    [Range(-90, 90)]
    public required decimal MinLatitude { get; init; }

    [Required]
    [Range(-90, 90)]
    public required decimal MaxLatitude { get; init; }

    [Required]
    [Range(-180, 180)]
    public required decimal MinLongitude { get; init; }

    [Required]
    [Range(-180, 180)]
    public required decimal MaxLongitude { get; init; }

    [Required]
    [Range(LocationClusterRules.MinZoom, LocationClusterRules.MaxZoom)]
    [SwaggerSchema(Description = "Map zoom level used for clustering (2–4)")]
    public required int Zoom { get; init; }
}

public record MapPinClusterPointDto(long Id, decimal Latitude, decimal Longitude);

public record LocationClusterStoreRequestDto
{
    [Required]
    public required decimal MinLatitude { get; init; }

    [Required]
    public required decimal MaxLatitude { get; init; }

    [Required]
    public required decimal MinLongitude { get; init; }

    [Required]
    public required decimal MaxLongitude { get; init; }

    [Required]
    [Range(LocationClusterRules.MinZoom, LocationClusterRules.MaxZoom)]
    public required int Zoom { get; init; }

    [Required]
    public required IReadOnlyList<MapClusterGetResponseDto> Clusters { get; init; }
}

public static class LocationClusterRules
{
    public const int MinZoom = 2;
    public const int MaxZoom = 4;
}
