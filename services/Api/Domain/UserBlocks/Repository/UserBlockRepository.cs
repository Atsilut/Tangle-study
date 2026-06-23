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

        public Task<bool> AnyBlockExistsBetweenUserAndOthersAsync(long userId, IReadOnlyCollection<long> otherUserIds)
        {
            if (otherUserIds.Count == 0) return Task.FromResult(false);

            return _context.UserBlocks.AnyAsync(b =>
                (b.BlockerId == userId && otherUserIds.Contains(b.BlockedUserId))
                || (otherUserIds.Contains(b.BlockerId) && b.BlockedUserId == userId));
        }

        public async Task<HashSet<long>> GetMutuallyBlockedUserIdsAsync(long userId, IReadOnlyCollection<long> otherUserIds)
        {
            if (otherUserIds.Count == 0) return [];

            var ids = otherUserIds.Distinct().ToList();
            var blockedIds = await _context.UserBlocks
                .Where(b => (b.BlockerId == userId && ids.Contains(b.BlockedUserId))
                    || (ids.Contains(b.BlockerId) && b.BlockedUserId == userId))
                .Select(b => b.BlockerId == userId ? b.BlockedUserId : b.BlockerId)
                .Distinct()
                .ToListAsync();

            return blockedIds.ToHashSet();
        }

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
