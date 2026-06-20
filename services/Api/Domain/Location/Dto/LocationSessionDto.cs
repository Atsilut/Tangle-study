using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Location.Dto;

public record LocationSessionCreateRequestDto
{
    [Required]
    public required long GroupId { get; init; }

    [Required]
    [Range(-90, 90)]
    [SwaggerSchema(Description = "Initial latitude in decimal degrees")]
    [DefaultValue(37.5665)]
    public required decimal Latitude { get; init; }

    [Required]
    [Range(-180, 180)]
    [SwaggerSchema(Description = "Initial longitude in decimal degrees")]
    [DefaultValue(126.9780)]
    public required decimal Longitude { get; init; }
}

public record LocationPositionUpdateRequestDto
{
    [Required]
    [Range(-90, 90)]
    public required decimal Latitude { get; init; }

    [Required]
    [Range(-180, 180)]
    public required decimal Longitude { get; init; }
}

public record LocationSessionGetResponseDto(
    long Id,
    long GroupId,
    long UserId,
    string UserNickname,
    decimal Latitude,
    decimal Longitude,
    DateTime StartedAt,
    DateTime PositionUpdatedAt);

public record LiveLocationGetResponseDto(
    long SessionId,
    long GroupId,
    long UserId,
    string UserNickname,
    decimal Latitude,
    decimal Longitude,
    DateTime UpdatedAt);

public record GroupMemberLocationStatusDto(
    long UserId,
    string UserNickname,
    bool IsSharing,
    long? SessionId,
    decimal? Latitude,
    decimal? Longitude,
    DateTime? UpdatedAt);

public record LocationSessionEndedDto(
    long SessionId,
    long GroupId,
    long UserId);
