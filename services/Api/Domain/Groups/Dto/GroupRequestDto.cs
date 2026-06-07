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
        public required string Name { get; init; }

        [Required]
        [StringLength(500)]
        [SwaggerSchema(Description = "Group description")]
        [DefaultValue("A group for Tangle developers.")]
        public required string Description { get; init; }

        [Required]
        [SwaggerSchema(Description = "Group visibility (Private = 0, Public = 1)")]
        [DefaultValue(GroupVisibility.Private)]
        public required GroupVisibility Visibility { get; init; }

        [SwaggerSchema(Description = "How users may join (Open = 0, Requestable = 1, InvitationOnly = 2). Defaults to Requestable when omitted.")]
        [DefaultValue(GroupJoinPolicy.Requestable)]
        public GroupJoinPolicy? JoinPolicy { get; init; }
    }

    public record GroupPatchRequestDto
    {
        [Required]
        [SwaggerSchema(Description = "Group id")]
        public required long Id { get; init; }

        [Required]
        [StringLength(50, MinimumLength = 1)]
        [SwaggerSchema(Description = "Group name")]
        public required string Name { get; init; }

        [Required]
        [StringLength(500)]
        [SwaggerSchema(Description = "Group description")]
        public required string Description { get; init; }

        [Required]
        [SwaggerSchema(Description = "Group visibility (Private = 0, Public = 1)")]
        public required GroupVisibility Visibility { get; init; }

        [Required]
        [SwaggerSchema(Description = "How users may join (Open = 0, Requestable = 1, InvitationOnly = 2)")]
        public required GroupJoinPolicy JoinPolicy { get; init; }
    }

    public record GroupTransferOwnershipRequestDto
    {
        [Required]
        [SwaggerSchema(Description = "Group id")]
        public required long Id { get; init; }

        [Required]
        [SwaggerSchema(Description = "Target user id to receive ownership")]
        public required long NewOwnerUserId { get; init; }
    }
}
