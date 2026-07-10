using Group.Entities;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace Group.Dto
{
    public record GroupMemberRolePatchRequestDto
    {
        [Required]
        [SwaggerSchema(Description = "Target role (Member = 0, Admin = 1). Owner cannot be set directly; use transfer ownership.")]
        public required GroupRole Role { get; init; }
    }
}
