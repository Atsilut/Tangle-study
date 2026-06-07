using Api.Domain.Friendships.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Friendships.Repository
{
    [Repository]
    public class FriendshipRepository(AppDbContext context) : IFriendshipRepository
    {
        private readonly AppDbContext _context = context;

        public Task CreateFriendshipAsync(Friendship friendship)
        {
            _context.Friendships.Add(friendship);
            return _context.SaveChangesAsync();
        }

        public Task<Friendship?> GetFriendshipByIdAsync(long id) =>
            _context.Friendships.FindAsync(id).AsTask();

        public Task<Friendship?> GetForUserPairAsync(long userId, long otherUserId)
        {
            var userLowId = Math.Min(userId, otherUserId);
            var userHighId = Math.Max(userId, otherUserId);
            return _context.Friendships.FirstOrDefaultAsync(f =>
                f.UserLowId == userLowId && f.UserHighId == userHighId);
        }

        public Task<bool> ExistsFriendshipForUserPairAsync(long userId, long otherUserId)
        {
            var userLowId = Math.Min(userId, otherUserId);
            var userHighId = Math.Max(userId, otherUserId);
            return _context.Friendships.AnyAsync(f =>
                f.UserLowId == userLowId && f.UserHighId == userHighId);
        }

        public Task<List<Friendship>> GetAllForUserAsync(long userId) =>
            _context.Friendships
                .Where(f => f.UserLowId == userId || f.UserHighId == userId)
                .ToListAsync();

        public Task DeleteFriendshipAsync(Friendship friendship)
        {
            _context.Friendships.Remove(friendship);
            return _context.SaveChangesAsync();
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
