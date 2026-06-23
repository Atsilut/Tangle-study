using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Repository;

namespace Api.Tests.Repositories;

public sealed class FakeFriendRequestRepository : IFriendRequestRepository
{
    private readonly List<FriendRequest> _requests = [];
    private long _nextId = 1;

    public Task CreateFriendRequestAsync(FriendRequest friendRequest)
    {
        if (_requests.Exists(r => InvolvesSameUserPair(r, friendRequest.RequesterId, friendRequest.AddresseeId)))
            throw new InvalidOperationException("A friend request already exists for this user pair.");

        typeof(FriendRequest)
            .GetProperty(nameof(FriendRequest.Id))!
            .SetValue(friendRequest, _nextId++);
        _requests.Add(friendRequest);
        return Task.CompletedTask;
    }

    private static bool InvolvesSameUserPair(FriendRequest request, long userId, long otherUserId) =>
        (request.RequesterId == userId && request.AddresseeId == otherUserId) ||
        (request.RequesterId == otherUserId && request.AddresseeId == userId);

    public Task<FriendRequest?> GetFriendRequestByIdAsync(long id) =>
        Task.FromResult(_requests.FirstOrDefault(r => r.Id == id));

    public Task<FriendRequest?> GetForUserPairAsync(long userId, long otherUserId) =>
        Task.FromResult(_requests
            .Where(r => InvolvesSameUserPair(r, userId, otherUserId))
            .OrderBy(r => r.Id)
            .FirstOrDefault());

    public Task<List<FriendRequest>> GetForUserAsync(long userId, bool? isPending = null)
    {
        var query = _requests.Where(r => r.RequesterId == userId || r.AddresseeId == userId);
        if (isPending.HasValue) query = query.Where(r => r.IsPending == isPending.Value);
        return Task.FromResult(query.ToList());
    }

    public Task UpdateFriendRequestAsync(FriendRequest friendRequest) => Task.CompletedTask;

    public Task DeleteFriendRequestAsync(FriendRequest friendRequest)
    {
        _requests.Remove(friendRequest);
        return Task.CompletedTask;
    }

    public Task DeleteAllForUserPairAsync(long userId, long otherUserId)
    {
        _requests.RemoveAll(r =>
            (r.RequesterId == userId && r.AddresseeId == otherUserId) ||
            (r.RequesterId == otherUserId && r.AddresseeId == userId));
        return Task.CompletedTask;
    }
}
