using System.ComponentModel.DataAnnotations;
using Api.Domain.Groups.Domain;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Groups.Dto
{
    public record GroupBoardCreateRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public required string Name { get; init; }

        public string? Description { get; init; }

        [SwaggerSchema(Description = "Omit to default: public group → ForAll, private group → MembersOnly")]
        public BoardVisibility? Visibility { get; init; }
    }

    public record GroupBoardPatchRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public required string Name { get; init; }

        public string? Description { get; init; }

        [Required]
        public required BoardVisibility Visibility { get; init; }
    }
}
