namespace Api.Domain.Posts.Dto
{
    public record PostGetResponseDto(long Id, string Title, string Content, DateTime CreatedAt, DateTime UpdatedAt, long AuthorId, string AuthorNickname);
    public record PostPatchResponseDto(string Title, string Content, DateTime UpdatedAt);
}
