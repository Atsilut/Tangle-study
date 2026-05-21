using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Repository;

namespace Api.Tests.Repositories;

public sealed class FakeFriendRequestRepository : IFriendRequestRepository
{
    private readonly List<FriendRequest> _requests = new();
    private long _nextId = 1;

    public Task CreateAsync(FriendRequest friendRequest)
    {
        typeof(FriendRequest)
            .GetProperty(nameof(FriendRequest.Id))!
            .SetValue(friendRequest, _nextId++);
        _requests.Add(friendRequest);
        return Task.CompletedTask;
    }

    public Task<FriendRequest?> GetByIdAsync(long id) =>
        Task.FromResult(_requests.FirstOrDefault(r => r.Id == id));

    public Task<FriendRequest?> GetBetweenAsync(long userAId, long userBId) =>
        Task.FromResult(_requests.FirstOrDefault(r =>
            (r.RequesterId == userAId && r.AddresseeId == userBId) ||
            (r.RequesterId == userBId && r.AddresseeId == userAId)));

    public Task<bool> ExistsFriendRequestBetweenAsync(long userAId, long userBId) =>
        Task.FromResult(_requests.Any(r =>
            (r.RequesterId == userAId && r.AddresseeId == userBId) ||
            (r.RequesterId == userBId && r.AddresseeId == userAId)));

    public Task<List<FriendRequest>> GetForUserAsync(long userId, bool? isPending = null)
    {
        var query = _requests.Where(r => r.RequesterId == userId || r.AddresseeId == userId);
        if (isPending.HasValue)
            query = query.Where(r => r.IsPending == isPending.Value);
        return Task.FromResult(query.ToList());
    }

    public Task UpdateAsync(FriendRequest friendRequest) => Task.CompletedTask;

    public Task DeleteAsync(FriendRequest friendRequest)
    {
        _requests.Remove(friendRequest);
        return Task.CompletedTask;
    }

    public Task DeleteAllBetweenAsync(long userAId, long userBId)
    {
        _requests.RemoveAll(r =>
            (r.RequesterId == userAId && r.AddresseeId == userBId) ||
            (r.RequesterId == userBId && r.AddresseeId == userAId));
        return Task.CompletedTask;
    }

    public Task DeleteAllForUserAsync(long userId)
    {
        _requests.RemoveAll(r => r.RequesterId == userId || r.AddresseeId == userId);
        return Task.CompletedTask;
    }
}
