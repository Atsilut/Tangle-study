using Api.Domain.Groups.Domain;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Groups.Dto
{
    public record GroupCreateRequestDto
    {
        [Required]
        [StringLength(50, MinimumLength = 1)]
        [SwaggerSchema(Description = "Group name")]
        [DefaultValue("Tangle Devs")]
        public string Name { get; init; }

        [Required]
        [StringLength(500)]
        [SwaggerSchema(Description = "Group description")]
        [DefaultValue("A group for Tangle developers.")]
        public string Description { get; init; }

        [Required]
        [SwaggerSchema(Description = "Group visibility (Private = 0, Public = 1)")]
        [DefaultValue(GroupVisibility.Private)]
        public GroupVisibility Visibility { get; init; }
    }

    public record GroupPatchRequestDto
    {
        [Required]
        [SwaggerSchema(Description = "Group id")]
        public long Id { get; init; }

        [Required]
        [StringLength(50, MinimumLength = 1)]
        [SwaggerSchema(Description = "Group name")]
        public string Name { get; init; }

        [Required]
        [StringLength(500)]
        [SwaggerSchema(Description = "Group description")]
        public string Description { get; init; }

        [Required]
        [SwaggerSchema(Description = "Group visibility (Private = 0, Public = 1)")]
        public GroupVisibility Visibility { get; init; }
    }

    public record GroupTransferOwnershipRequestDto
    {
        [Required]
        [SwaggerSchema(Description = "Group id")]
        public long Id { get; init; }

        [Required]
        [SwaggerSchema(Description = "Target user id to receive ownership")]
        public long NewOwnerUserId { get; init; }
    }
}
