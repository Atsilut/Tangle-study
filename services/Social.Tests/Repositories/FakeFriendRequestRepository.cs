using Social.Friendships.Domain;
using Social.Friendships.Repository;

namespace Social.Tests.Repositories;

public sealed class FakeFriendRequestRepository : IFriendRequestRepository
{
    private long _nextId = 1;
    public List<FriendRequest> Items { get; } = [];

    public Task CreateFriendRequestAsync(FriendRequest friendRequest)
    {
        typeof(FriendRequest).GetProperty(nameof(FriendRequest.Id))!
            .SetValue(friendRequest, _nextId++);
        Items.Add(friendRequest);
        return Task.CompletedTask;
    }

    public Task<FriendRequest?> GetFriendRequestByIdAsync(long id) =>
        Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

    public Task<FriendRequest?> GetForUserPairAsync(long userId, long otherUserId) =>
        Task.FromResult(Items.FirstOrDefault(r =>
            (r.RequesterId == userId && r.AddresseeId == otherUserId)
            || (r.RequesterId == otherUserId && r.AddresseeId == userId)));

    public Task<List<FriendRequest>> GetForUserAsync(long userId, bool? isPending = null)
    {
        var query = Items.Where(r => r.RequesterId == userId || r.AddresseeId == userId);
        if (isPending.HasValue)
            query = query.Where(r => r.IsPending == isPending.Value);
        return Task.FromResult(query.ToList());
    }

    public Task UpdateFriendRequestAsync(FriendRequest friendRequest) => Task.CompletedTask;

    public Task DeleteFriendRequestAsync(FriendRequest friendRequest)
    {
        Items.Remove(friendRequest);
        return Task.CompletedTask;
    }

    public Task DeleteAllForUserPairAsync(long userId, long otherUserId)
    {
        Items.RemoveAll(r =>
            (r.RequesterId == userId && r.AddresseeId == otherUserId)
            || (r.RequesterId == otherUserId && r.AddresseeId == userId));
        return Task.CompletedTask;
    }

    public Task DeleteAllForUserAsync(long userId)
    {
        Items.RemoveAll(r => r.RequesterId == userId || r.AddresseeId == userId);
        return Task.CompletedTask;
    }
}
