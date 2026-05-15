using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Comments.Dto
{
    public record CommentCreateRequestDto
    {
        [Required]
        public string Content { get; init; }
        [Required]
        public long PostId { get; init; }
    }
}
