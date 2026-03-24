using System.ComponentModel.DataAnnotations;

namespace Api.Domain.Users.Dto
{
    public record UserGetResponseDto(Guid Id, string Email, string Nickname);
}
