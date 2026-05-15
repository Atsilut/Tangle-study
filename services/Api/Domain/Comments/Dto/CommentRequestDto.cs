using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Comments.Dto
{
    public record CommentCreateRequestDto
    {
        [Required]
        [SwaggerSchema(Description = "Comment content")]
        [DefaultValue("Thanks for sharing your post.")]
        public string Content { get; init; }

        [Required]
        [SwaggerSchema(Description = "Post Id")]
        [DefaultValue(1)]
        public long PostId { get; init; }
    }
}