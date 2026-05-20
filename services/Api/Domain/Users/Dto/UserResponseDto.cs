namespace Api.Domain.Users.Dto
{
    public record UserGetResponseDto(long Id, string Email, string Nickname, DateTime CreatedAt, DateTime UpdatedAt);
    public record UserPatchResponseDto(string Nickname, DateTime UpdatedAt);
}
