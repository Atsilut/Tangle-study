using System.ComponentModel.DataAnnotations;

namespace Social.Dto;

public record UserBlockCreateRequestDto
{
    [Required]
    public required long BlockedUserId { get; init; }
}
