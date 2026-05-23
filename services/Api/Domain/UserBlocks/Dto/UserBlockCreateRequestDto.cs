using System.ComponentModel.DataAnnotations;

namespace Api.Domain.UserBlocks.Dto
{
    public record UserBlockCreateRequestDto
    {
        [Required]
        public long BlockedUserId { get; init; }
    }
}
