using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Posts.Client;
using Swashbuckle.AspNetCore.Annotations;

namespace Posts.Dto;

public record PostLocationGetResponseDto(decimal Latitude, decimal Longitude);

public record PostGetResponseDto(
    long Id,
    string Title,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    long AuthorId,
    string AuthorNickname,
    IReadOnlyList<MediaAssetGetResponseDto> Media,
    PostLocationGetResponseDto? Location);

public record PostPatchResponseDto(string Title, string Content, DateTime UpdatedAt);

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

    [SwaggerSchema(Description = "Ready media asset IDs to attach (post context; multiple allowed)")]
    public long[]? MediaAssetIds { get; init; }

    [SwaggerSchema(Description = "Optional latitude (-90 to 90); must be provided with Longitude")]
    public decimal? Latitude { get; init; }

    [SwaggerSchema(Description = "Optional longitude (-180 to 180); must be provided with Latitude")]
    public decimal? Longitude { get; init; }
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

    [SwaggerSchema(Description = "Ready media asset IDs to attach to the post")]
    public long[]? AddMediaAssetIds { get; init; }

    [SwaggerSchema(Description = "Media asset IDs currently on the post to delete")]
    public long[]? RemoveMediaAssetIds { get; init; }

    [SwaggerSchema(Description = "Optional latitude (-90 to 90); must be provided with Longitude")]
    public decimal? Latitude { get; init; }

    [SwaggerSchema(Description = "Optional longitude (-180 to 180); must be provided with Latitude")]
    public decimal? Longitude { get; init; }

    [SwaggerSchema(Description = "When true, removes any location linked to the post")]
    [DefaultValue(false)]
    public bool ClearLocation { get; init; }
}

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
