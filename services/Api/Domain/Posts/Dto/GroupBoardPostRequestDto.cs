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
    }
}
