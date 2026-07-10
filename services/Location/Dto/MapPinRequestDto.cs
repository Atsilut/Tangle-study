using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Location.Dto;

public record MapPinCreateRequestDto
{
    [Required]
    [Range(-90, 90)]
    [SwaggerSchema(Description = "Latitude in decimal degrees")]
    [DefaultValue(37.5665)]
    public required decimal Latitude { get; init; }

    [Required]
    [Range(-180, 180)]
    [SwaggerSchema(Description = "Longitude in decimal degrees")]
    [DefaultValue(126.9780)]
    public required decimal Longitude { get; init; }

    [SwaggerSchema(Description = "Optional post to link this pin to (caller must own the post)")]
    public long? PostId { get; init; }
}

public record MapPinBoundsQueryDto
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
}
