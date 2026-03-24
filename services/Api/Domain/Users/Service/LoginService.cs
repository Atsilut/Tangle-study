using Api.Domain.Users.Dto;
using Api.Domain.Users.Repository;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Api.Global.Security;

namespace Api.Domain.Users.Service
{
    [Service]
    public class LoginService
    {
        private readonly UserRepository _repo;
        private readonly TokenProvider _tokenProvider;

        public LoginService(UserRepository repo, TokenProvider tokenProvider)
        {
            _repo = repo;
            _tokenProvider = tokenProvider;
        }

        public async Task CreateUserAsync(UserCreateRequestDto request)
        {
            var foundUser = await _repo.GetByEmailAsync(request.Email);
            if (foundUser != null) throw new EntityAlreadyExistsException("User", request.Email);

            var encodedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var user = new Domain.User(
                email: request.Email,
                password: encodedPassword,
                nickname: request.Nickname
                );
            await _repo.CreateAsync(user);
        }

        public async Task<LoginResponseDto?> LoginUserAsync(LoginRequestDto request)
        {
            var foundUser = await _repo.GetByEmailAsync(request.Email);
            if (foundUser == null) return null;
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, foundUser.Password);
            if (!isPasswordValid) return null;

            var token = _tokenProvider.GenerateToken(foundUser.Id);
            return new LoginResponseDto(token);
        }

    }
}
