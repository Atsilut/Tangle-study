using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Groups.Dto
{
    public record GroupBlacklistCreateRequestDto
    {
        [Required]
        public required long UserId { get; init; }
    }
}
