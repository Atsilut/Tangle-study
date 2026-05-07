using Api.Domain.Users.Domain;

namespace Api.Domain.Users.Repository
{
    public interface IUserRepository
    {
        public Task CreateUserAsync(User user);
        public Task<List<User>?> GetAllUsersAsync();
        public Task<User?> GetUserByIdAsync(long id);
        public Task<User?> GetUserByEmailAsync(string email);
        Task<bool> ExistsUserByEmailAsync(string email);
        public Task<User?> GetUserByNicknameAsync(string nickname);
        Task<bool> ExistsUserByNicknameAsync(string nickname);
        public Task UpdateUserAsync(User user);
        public Task DeleteUserAsync(User user);
    }
}
