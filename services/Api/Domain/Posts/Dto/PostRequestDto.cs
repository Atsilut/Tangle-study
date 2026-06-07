using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Posts.Dto
{
    public record PostCreateRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        [SwaggerSchema(Description = "Post Title")]
        [DefaultValue("My Happy Marriage")]
        public required string Title { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        [SwaggerSchema(Description = "Post Content")]
        [DefaultValue("Will be started in Aus")]
        public required string Content { get; init; }

        [SwaggerSchema(Description = "Group Id (optional; for group posts)")]
        public long? GroupId { get; init; }

        [SwaggerSchema(Description = "Group board Id (optional; for group posts)")]
        public long? GroupBoardId { get; init; }
    }

    public record PostPatchRequestDto
    {
        [Required]
        [SwaggerSchema(Description = "Post Id")]
        public required long Id { get; init; }
        
        [Required]
        [StringLength(100, MinimumLength = 1)]
        [SwaggerSchema(Description = "Post Title")]
        [DefaultValue("My Happy Life")]
        public required string Title { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 1)]
        [SwaggerSchema(Description = "Post Content")]
        [DefaultValue("Will be started in somewhere")]
        public required string Content { get; init; }
    }
}
