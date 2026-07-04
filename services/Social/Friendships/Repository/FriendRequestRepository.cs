using Microsoft.EntityFrameworkCore;
using Social.Db;
using Social.Friendships.Domain;
using Social.Infrastructure;

namespace Social.Friendships.Repository;

[Repository]
public class FriendRequestRepository(SocialDbContext context) : IFriendRequestRepository
{
    private readonly SocialDbContext _context = context;

    public Task CreateFriendRequestAsync(FriendRequest friendRequest)
    {
        _context.FriendRequests.Add(friendRequest);
        return _context.SaveChangesAsync();
    }

    public Task<FriendRequest?> GetFriendRequestByIdAsync(long id) =>
        _context.FriendRequests.FindAsync(id).AsTask();

    public Task<FriendRequest?> GetForUserPairAsync(long userId, long otherUserId) =>
        _context.FriendRequests
            .Where(r =>
                (r.RequesterId == userId && r.AddresseeId == otherUserId) ||
                (r.RequesterId == otherUserId && r.AddresseeId == userId))
            .OrderBy(r => r.Id)
            .FirstOrDefaultAsync();

    public Task<List<FriendRequest>> GetForUserAsync(long userId, bool? isPending = null)
    {
        var query = _context.FriendRequests
            .Where(r => r.RequesterId == userId || r.AddresseeId == userId);
        if (isPending.HasValue)
            query = query.Where(r => r.IsPending == isPending.Value);
        return query.ToListAsync();
    }

    public Task UpdateFriendRequestAsync(FriendRequest friendRequest) => _context.SaveChangesAsync();

    public Task DeleteFriendRequestAsync(FriendRequest friendRequest)
    {
        _context.FriendRequests.Remove(friendRequest);
        return _context.SaveChangesAsync();
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

    public async Task DeleteAllForUserAsync(long userId)
    {
        var requests = await _context.FriendRequests
            .Where(r => r.RequesterId == userId || r.AddresseeId == userId)
            .ToListAsync();
        if (requests.Count == 0) return;
        _context.FriendRequests.RemoveRange(requests);
        await _context.SaveChangesAsync();
    }
}
