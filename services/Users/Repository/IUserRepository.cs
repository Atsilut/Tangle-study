using Users.Domain;

namespace Users.Repository
{
    public interface IUserRepository
    {
        public Task CreateUserAsync(User user);
        public Task<List<User>> GetAllUsersAsync();
        public Task<IReadOnlyDictionary<long, string>> GetNicknamesByIdsAsync(IEnumerable<long> ids);
        public Task<User?> GetUserByIdAsync(long id);
        public Task<bool> ExistsUserByIdAsync(long id);
        public Task<bool> AllUsersExistByIdsAsync(IReadOnlyCollection<long> ids);
        public Task<User?> GetUserByEmailAsync(string email);
        public Task<bool> ExistsUserByEmailAsync(string email);
        public Task<User?> GetUserByNicknameAsync(string nickname);
        public Task<bool> ExistsUserByNicknameAsync(string nickname);
        public Task UpdateUserAsync(User user);
        public Task DeleteUserAsync(User user);
    }
}
