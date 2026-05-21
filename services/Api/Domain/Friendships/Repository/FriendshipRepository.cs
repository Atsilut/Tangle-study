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

        public async Task CreateFriendshipAsync(Friendship friendship)
        {
            _context.Friendships.Add(friendship);
            await _context.SaveChangesAsync();
        }

        public async Task<Friendship?> GetFriendshipByIdAsync(long id) =>
            await _context.Friendships.FindAsync(id);

        public async Task<Friendship?> GetFriendshipBetweenAsync(long userAId, long userBId) =>
            await _context.Friendships.FirstOrDefaultAsync(f =>
                (f.RequesterId == userAId && f.AddresseeId == userBId) ||
                (f.RequesterId == userBId && f.AddresseeId == userAId));

        public async Task<List<Friendship>> GetFriendshipsForUserAsync(long userId, FriendshipStatus? status = null)
        {
            var query = _context.Friendships
                .Where(f => f.RequesterId == userId || f.AddresseeId == userId);
            if (status.HasValue)
                query = query.Where(f => f.Status == status.Value);
            return await query.ToListAsync();
        }

        public async Task UpdateFriendshipAsync(Friendship friendship) => await _context.SaveChangesAsync();

        public async Task DeleteFriendshipAsync(Friendship friendship)
        {
            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllFriendshipsForUserAsync(long userId)
        {
            var friendships = await _context.Friendships
                .Where(f => f.RequesterId == userId || f.AddresseeId == userId)
                .ToListAsync();
            if (friendships.Count == 0) return;
            _context.Friendships.RemoveRange(friendships);
            await _context.SaveChangesAsync();
        }
    }
}
