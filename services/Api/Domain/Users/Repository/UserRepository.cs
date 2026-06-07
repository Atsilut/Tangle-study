using Api.Domain.Users.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Users.Repository
{
    [Repository]
    public class UserRepository(AppDbContext context) : IUserRepository
    {
        private readonly AppDbContext _context = context;

        public Task CreateUserAsync(User user)
        {
            _context.Users.Add(user);
            return _context.SaveChangesAsync();
        }

        public Task<List<User>> GetAllUsersAsync() => _context.Users.ToListAsync();

        public Task<IReadOnlyDictionary<long, string>> GetNicknamesByIdsAsync(IEnumerable<long> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0)
                return Task.FromResult<IReadOnlyDictionary<long, string>>(new Dictionary<long, string>());

            return QueryNicknamesByIdsAsync(idList);
        }

        private async Task<IReadOnlyDictionary<long, string>> QueryNicknamesByIdsAsync(List<long> idList) =>
            await _context.Users
                .Where(u => idList.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Nickname);

        public Task<User?> GetUserByIdAsync(long id) => _context.Users.FindAsync(id).AsTask();

        public Task<bool> ExistsUserByIdAsync(long id) =>
            _context.Users.AnyAsync(u => u.Id == id);
        public Task<User?> GetUserByEmailAsync(string email) 
            => _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        public Task<bool> ExistsUserByEmailAsync(string email) =>
            _context.Users.AnyAsync(u => u.Email == email);

        public Task<User?> GetUserByNicknameAsync(string nickname)
            => _context.Users.FirstOrDefaultAsync(u => u.Nickname == nickname);

        public Task<bool> ExistsUserByNicknameAsync(string nickname) =>
            _context.Users.AnyAsync(u => u.Nickname == nickname);

        public Task UpdateUserAsync(User user) => _context.SaveChangesAsync();

        public Task DeleteUserAsync(User user)
        {
            _context.Users.Remove(user);
            return _context.SaveChangesAsync();
        }
    }
}