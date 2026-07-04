using System.ComponentModel.DataAnnotations;

namespace Group.Dto
{
    public record GroupBlacklistCreateRequestDto
    {
        [Required]
        public required long UserId { get; init; }
    }
}
