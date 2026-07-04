using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Community.Client;
using Swashbuckle.AspNetCore.Annotations;

namespace Community.Dto;

public record CommentCreateRequestDto
{
    [MaxLength(1000)]
    [SwaggerSchema(Description = "Comment content (optional when MediaAssetId is set)")]
    [DefaultValue("Thanks for sharing your post.")]
    public string Content { get; init; } = string.Empty;

    [Required]
    [SwaggerSchema(Description = "Post Id")]
    [DefaultValue(1)]
    public required long PostId { get; init; }

    [SwaggerSchema(Description = "Parent Comment Id (optional)")]
    public long? ParentId { get; init; }

    [SwaggerSchema(Description = "Ready media asset ID to attach (comment context; single file only)")]
    public long? MediaAssetId { get; init; }
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

public record CommentGetResponseDto
{
    [Required]
    public required long Id { get; set; }

    [Required]
    public required string Content { get; set; }

    public long? PostId { get; set; }

    public long? DeletedPostId { get; set; }

    public long AuthorId { get; set; }

    [Required]
    public required string AuthorNickname { get; set; }

    public long? UserId { get; set; }

    public long? DeletedUserId { get; set; }

    public long? ParentId { get; set; }

    public long? DeletedParentId { get; set; }

    [Required]
    public required DateTime CreatedAt { get; set; }

    [Required]
    public required DateTime UpdatedAt { get; set; }

    public List<CommentGetResponseDto> Replies { get; set; } = [];

    public MediaAssetGetResponseDto? Media { get; set; }
}

public record CommentPatchResponseDto
{
    [Required]
    public required string Content { get; set; }

    public long? PostId { get; set; }

    public long? DeletedPostId { get; set; }

    [Required]
    public required DateTime UpdatedAt { get; set; }
}
