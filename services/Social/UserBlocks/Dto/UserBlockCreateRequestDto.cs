using System.ComponentModel.DataAnnotations;

namespace Social.UserBlocks.Dto;

public record UserBlockCreateRequestDto
{
    [Required]
    public required long BlockedUserId { get; init; }
}
