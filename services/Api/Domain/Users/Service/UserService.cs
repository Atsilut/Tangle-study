using Api.Domain.Users.Dto;
using Api.Domain.Users.Repository;

namespace Api.Domain.Users.Service
{
    public class UserService
    {
        private readonly UserRepository _repo;

        public UserService(UserRepository repo)
        {
            _repo = repo;
        }

        public async Task CreateUserAsync(CreateUserDto createUserDto)
        {
            var user = new Domain.User(
                Guid.NewGuid(), 
                createUserDto.Email, 
                createUserDto.Password, 
                createUserDto.Nickname
                );
            await _repo.CreateAsync(user);
        }

    }
}
