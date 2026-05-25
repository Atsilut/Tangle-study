using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Groups.Dto
{
    public record GroupInvitationCreateRequestDto
    {
        [Required]
        [SwaggerSchema(Description = "Target user id to invite")]
        public long InviteeId { get; init; }
    }
}
