using Api.Domain.Users.Domain;

namespace Api.Domain.Users.Repository
{
    public interface IUserRepository
    {
        public Task CreateUserAsync(User user);
        public Task<List<User>?> GetAllAsync();
        public Task<User?> GetByIdAsync(long id);
        public Task<User?> GetByEmailAsync(string email);
        public Task UpdateUserAsync(User user);
        public Task DeleteUserAsync(User user);
    }
}
