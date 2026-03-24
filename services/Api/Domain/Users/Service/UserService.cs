using Api.Domain.Users.Domain;
using Api.Domain.Users.Dto;
using Api.Domain.Users.Repository;
using Api.Global.Infrastructure;

namespace Api.Domain.Users.Service
{
    [Service]
    public class UserService
    {
        private readonly UserRepository _repo;

        public UserService(UserRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<User>?> GetAllUsersAsync()
        {
            var users = await _repo.GetAllAsync();
            return users;
        }

        public async Task<UserGetResponseDto?> GetUserByIdAsync(Guid id)
        {
            var user = await _repo.GetByIdAsync(id);
            if (user == null) return null;

            var userResponse = new UserGetResponseDto(
                Id: user.Id,
                Email: user.Email,
                Nickname: user.Nickname
            );

            return userResponse;
        }
    }
}