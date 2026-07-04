using System.ComponentModel.DataAnnotations;
using Group.Entities;
using Swashbuckle.AspNetCore.Annotations;

namespace Group.Dto
{
    public record GroupBoardCreateRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public required string Name { get; init; }

        public string? Description { get; init; }

        [SwaggerSchema(Description = "Omit to default: public group → ForAll, private group → MembersOnly")]
        public BoardVisibility? Visibility { get; init; }

        [SwaggerSchema(Description = "Omit to default: MembersOnly")]
        public BoardWriteability? Writeability { get; init; }
    }

    public record GroupBoardPatchRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public required string Name { get; init; }

        public string? Description { get; init; }

        [Required]
        public required BoardVisibility Visibility { get; init; }

        [Required]
        public required BoardWriteability Writeability { get; init; }
    }
}
