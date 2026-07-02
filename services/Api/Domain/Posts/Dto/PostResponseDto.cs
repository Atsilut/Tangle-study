using Api.Client;

namespace Api.Domain.Posts.Dto
{
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
}
