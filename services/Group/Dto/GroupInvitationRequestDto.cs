using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace Group.Dto
{
    public record GroupInvitationCreateRequestDto
    {
        [Required]
        [SwaggerSchema(Description = "Target user id to invite")]
        public required long InviteeId { get; init; }
    }
}
