using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Users.Dto
{
    public record UserGetResponseDto(long Id, string Email, string Nickname);
    public record UserPatchResponseDto(string Nickname);
}
