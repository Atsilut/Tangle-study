namespace Api.Domain.Posts.Dto
{
    public record PostGetResponseDto(long Id, string Title, string Content, long UserId);
}
