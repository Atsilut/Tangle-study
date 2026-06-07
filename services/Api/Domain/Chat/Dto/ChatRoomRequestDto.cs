using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Chat.Dto;

public record ChatRoomDirectCreateRequestDto
{
    [Required]
    [SwaggerSchema(Description = "The other user in the 1:1 chat (must be a friend)")]
    public long OtherUserId { get; init; }
}

public record ChatRoomMultiCreateRequestDto
{
    [MaxLength(200)]
    [SwaggerSchema(Description = "Optional room title")]
    public string? Title { get; init; }

    [Required]
    [MinLength(1)]
    [SwaggerSchema(Description = "User ids to include besides yourself (you are added automatically as owner)")]
    public IReadOnlyList<long> ParticipantUserIds { get; init; } = [];
}

public record ChatRoomPlatformGroupCreateRequestDto
{
    [MaxLength(200)]
    public string? Title { get; init; }

    [Required]
    [MinLength(1)]
    [SwaggerSchema(Description = "Initial participants (must be members of the platform group; you are added automatically as owner)")]
    public IReadOnlyList<long> ParticipantUserIds { get; init; } = [];
}

public record ChatRoomParticipantAddRequestDto
{
    [Required]
    public long UserId { get; init; }
}
