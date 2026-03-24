using Api.Domain.Users.Dto;
using Api.Domain.Users.Repository;

namespace Api.Domain.Users.Service
{
    public class LoginService
    {
        private readonly UserRepository _repo;

        public LoginService(UserRepository repo)
        {
            _repo = repo;
        }

        public async Task CreateUserAsync(UserCreateRequestDto request)
        {
            var encodedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var user = new Domain.User(
                id: Guid.NewGuid(),
                email: request.Email,
                password: encodedPassword,
                nickname: request.Nickname
                );
            await _repo.CreateAsync(user);
        }
    }
}
