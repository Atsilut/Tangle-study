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
            var user = new Domain.User(
                Guid.NewGuid(),
                request.Email,
                request.Password,
                request.Nickname
                );
            await _repo.CreateAsync(user);
        }
    }
}
