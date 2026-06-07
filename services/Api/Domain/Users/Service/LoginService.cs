using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Domain.Users.Repository;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Api.Global.Security;

namespace Api.Domain.Users.Service
{
    [Service]
    public class LoginService(IUserRepository repo, TokenProvider tokenProvider)
    {
        private readonly IUserRepository _repo = repo;
        private readonly TokenProvider _tokenProvider = tokenProvider;

        public async Task CreateUserAsync(UserCreateRequestDto request)
        {
            var isUserExist = await _repo.ExistsUserByEmailAsync(request.Email);
            if (isUserExist)
                throw new EntityAlreadyExistsException($"A user with email '{request.Email}' already exists.");

            var encodedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var user = new User(
                email: request.Email,
                password: encodedPassword,
                nickname: request.Nickname
                );
            await _repo.CreateUserAsync(user);
        }

        public async Task<LoginResponseDto?> LoginUserAsync(LoginRequestDto request)
        {
            var foundUser = await _repo.GetUserByEmailAsync(request.Email);
            if (foundUser == null) return null;
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, foundUser.Password);
            if (!isPasswordValid) return null;

            var token = _tokenProvider.GenerateToken(foundUser.Id);
            return new LoginResponseDto(token);
        }

    }
}
