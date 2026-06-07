using Api.Domain.UserBlocks.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.UserBlocks.Repository
{
    [Repository]
    public class UserBlockRepository(AppDbContext context) : IUserBlockRepository
    {
        private readonly AppDbContext _context = context;

        public Task CreateUserBlockAsync(UserBlock userBlock)
        {
            _context.UserBlocks.Add(userBlock);
            return _context.SaveChangesAsync();
        }

        public Task<bool> ExistsUserBlockAsync(long blockerId, long blockedUserId) =>
            _context.UserBlocks.AnyAsync(b =>
                b.BlockerId == blockerId && b.BlockedUserId == blockedUserId);

        public Task<UserBlock?> GetUserBlockByIdAsync(long id) =>
            _context.UserBlocks.FindAsync(id).AsTask();

        public Task<List<UserBlock>> GetAllForBlockerAsync(long blockerId) =>
            _context.UserBlocks
                .Where(b => b.BlockerId == blockerId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

        public Task DeleteUserBlockAsync(UserBlock userBlock)
        {
            _context.UserBlocks.Remove(userBlock);
            return _context.SaveChangesAsync();
        }
    }
}
