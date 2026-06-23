using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Posts.Dto
{
    public record GroupBoardPostCreateRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        [SwaggerSchema(Description = "Post Title")]
        [DefaultValue("Group post title")]
        public required string Title { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        [SwaggerSchema(Description = "Post Content")]
        [DefaultValue("Group post content")]
        public required string Content { get; init; }

        [SwaggerSchema(Description = "Ready media asset IDs to attach (post context; multiple allowed)")]
        public long[]? MediaAssetIds { get; init; }

        [SwaggerSchema(Description = "Optional latitude (-90 to 90); must be provided with Longitude")]
        public decimal? Latitude { get; init; }

        [SwaggerSchema(Description = "Optional longitude (-180 to 180); must be provided with Latitude")]
        public decimal? Longitude { get; init; }
    }
}
