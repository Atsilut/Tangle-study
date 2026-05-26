using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Groups.Dto
{
    public record GroupBlacklistCreateRequestDto
    {
        [Required]
        public long UserId { get; init; }
    }
}
