using Api.Domain.UserBlocks.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.UserBlocks.Repository
{
    [Repository]
    public class UserBlockRepository : IUserBlockRepository
    {
        private readonly AppDbContext _context;

        public UserBlockRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(UserBlock userBlock)
        {
            _context.UserBlocks.Add(userBlock);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(long blockerId, long blockedUserId) =>
            await _context.UserBlocks.AnyAsync(b =>
                b.BlockerId == blockerId && b.BlockedUserId == blockedUserId);

        public async Task<UserBlock?> GetByIdAsync(long id) =>
            await _context.UserBlocks.FindAsync(id);

        public async Task<List<UserBlock>> GetAllForBlockerAsync(long blockerId) =>
            await _context.UserBlocks
                .Where(b => b.BlockerId == blockerId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

        public async Task DeleteAsync(UserBlock userBlock)
        {
            _context.UserBlocks.Remove(userBlock);
            await _context.SaveChangesAsync();
        }
    }
}
