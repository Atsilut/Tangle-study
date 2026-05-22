using Api.Domain.Friendships.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Friendships.Repository
{
    [Repository]
    public class FriendRequestRepository : IFriendRequestRepository
    {
        private readonly AppDbContext _context;

        public FriendRequestRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(FriendRequest friendRequest)
        {
            _context.FriendRequests.Add(friendRequest);
            await _context.SaveChangesAsync();
        }

        public async Task<FriendRequest?> GetByIdAsync(long id) =>
            await _context.FriendRequests.FindAsync(id);

        public async Task<FriendRequest?> GetForUserPairAsync(long userId, long otherUserId) =>
            await _context.FriendRequests.FirstOrDefaultAsync(r =>
                (r.RequesterId == userId && r.AddresseeId == otherUserId) ||
                (r.RequesterId == otherUserId && r.AddresseeId == userId));

        public async Task<List<FriendRequest>> GetForUserAsync(long userId, bool? isPending = null)
        {
            var query = _context.FriendRequests
                .Where(r => r.RequesterId == userId || r.AddresseeId == userId);
            if (isPending.HasValue)
                query = query.Where(r => r.IsPending == isPending.Value);
            return await query.ToListAsync();
        }

        public async Task UpdateAsync(FriendRequest friendRequest) => await _context.SaveChangesAsync();

        public async Task DeleteAsync(FriendRequest friendRequest)
        {
            _context.FriendRequests.Remove(friendRequest);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllForUserPairAsync(long userId, long otherUserId)
        {
            var requests = await _context.FriendRequests
                .Where(r =>
                    (r.RequesterId == userId && r.AddresseeId == otherUserId) ||
                    (r.RequesterId == otherUserId && r.AddresseeId == userId))
                .ToListAsync();
            if (requests.Count == 0) return;
            _context.FriendRequests.RemoveRange(requests);
            await _context.SaveChangesAsync();
        }
    }
}
