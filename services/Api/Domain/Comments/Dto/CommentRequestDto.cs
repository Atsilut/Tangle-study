using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Comments.Dto
{
    public record CommentCreateRequestDto
    {
        [Required]
        [StringLength(1000, MinimumLength = 1)]
        [SwaggerSchema(Description = "Comment content")]
        [DefaultValue("Thanks for sharing your post.")]
        public required string Content { get; init; }

        [Required]
        [SwaggerSchema(Description = "Post Id")]
        [DefaultValue(1)]
        public required long PostId { get; init; }

        [SwaggerSchema(Description = "Parent Comment Id (optional)")]
        public long? ParentId { get; init; }
    }

    public record CommentPatchRequestDto
    {
        [Required]
        [SwaggerSchema(Description = "Comment Id")]
        [DefaultValue(1)]
        public required long Id { get; init; }
        [Required]
        [StringLength(1000, MinimumLength = 1)]
        [SwaggerSchema(Description = "Updated Comment content")]
        [DefaultValue("This content has been changed.")]
        public required string Content { get; init; }
    }
}
