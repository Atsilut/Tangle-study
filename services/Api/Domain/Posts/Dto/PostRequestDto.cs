using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Posts.Dto
{
    public class PostCreateRequestDto
    {
        [Required]
        [SwaggerSchema(Description = "Post Title")]
        [DefaultValue("My Happy Marriage")]
        public string Title { get; init; }

        [Required]
        [SwaggerSchema(Description = "Post Content")]
        [DefaultValue("Will be started in Aus")]
        public string Content { get; init; }
    }
}
