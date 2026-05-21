using Api.Domain.Friendships.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Friendships.Repository
{
    [Repository]
    public class FriendshipRepository : IFriendshipRepository
    {
        private readonly AppDbContext _context;

        public FriendshipRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(Friendship friendship)
        {
            _context.Friendships.Add(friendship);
            await _context.SaveChangesAsync();
        }

        public async Task<Friendship?> GetByIdAsync(long id) =>
            await _context.Friendships.FindAsync(id);

        public async Task<Friendship?> GetBetweenAsync(long userAId, long userBId)
        {
            var userLowId = Math.Min(userAId, userBId);
            var userHighId = Math.Max(userAId, userBId);
            return await _context.Friendships.FirstOrDefaultAsync(f =>
                f.UserLowId == userLowId && f.UserHighId == userHighId);
        }

        public async Task<bool> ExistsFriendshipBetweenAsync(long userAId, long userBId)
        {
            var userLowId = Math.Min(userAId, userBId);
            var userHighId = Math.Max(userAId, userBId);
            return await _context.Friendships.AnyAsync(f =>
                f.UserLowId == userLowId && f.UserHighId == userHighId);
        }

        public async Task<List<Friendship>> GetAllForUserAsync(long userId) =>
            await _context.Friendships
                .Where(f => f.UserLowId == userId || f.UserHighId == userId)
                .ToListAsync();

        public async Task DeleteAsync(Friendship friendship)
        {
            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllForUserAsync(long userId)
        {
            var friendships = await _context.Friendships
                .Where(f => f.UserLowId == userId || f.UserHighId == userId)
                .ToListAsync();
            if (friendships.Count == 0) return;
            _context.Friendships.RemoveRange(friendships);
            await _context.SaveChangesAsync();
        }
    }
}
