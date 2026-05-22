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
    }
}
