using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Comments.Dto
{
    public record CommentGetResponseDto
    {
        [Required]
        public long Id { get; set; }

        [Required]
        public string Content { get; set; }

        [Required]
        public long PostId { get; set; }

        [Required]
        public long UserId { get; set; }

        public long? ParentId { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }

        public List<CommentGetResponseDto> Replies { get; set; } = [];
    }

    public record CommentPatchResponseDto
    {
        [Required]
        public string Content { get; set; }

        [Required]
        public long PostId { get; set; }

        [Required]
        public DateTime UpdatedAt { get; set; }
    }
}
