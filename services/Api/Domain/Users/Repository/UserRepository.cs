using Api.Domain.Users.Domain;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Users.Repository
{
    [Repository]
    public class UserRepository
    {
        private readonly Global.Db.AppDbContext _context;

        public UserRepository(Global.Db.AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        public async Task<List<User>?> GetAllAsync() => await _context.Users.ToListAsync();

        public async Task<User?> GetByIdAsync(long id) => await _context.Users.FindAsync(id);
        public async Task<User?> GetByEmailAsync(string email) 
            => await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        public async Task UpdateUserAsync(User user) {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteUserAsync(User user)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }
}