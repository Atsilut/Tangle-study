using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Repository;

namespace Api.Tests.Repositories;

public sealed class FakeFriendshipRepository : IFriendshipRepository
{
    private readonly List<Friendship> _friendships = new();
    private long _nextId = 1;

    public Task CreateFriendshipAsync(Friendship friendship)
    {
        typeof(Friendship)
            .GetProperty(nameof(Friendship.Id))!
            .SetValue(friendship, _nextId++);
        _friendships.Add(friendship);
        return Task.CompletedTask;
    }

    public Task<Friendship?> GetFriendshipByIdAsync(long id) =>
        Task.FromResult(_friendships.FirstOrDefault(f => f.Id == id));

    public Task<Friendship?> GetFriendshipBetweenAsync(long userAId, long userBId) =>
        Task.FromResult(_friendships.FirstOrDefault(f =>
            (f.RequesterId == userAId && f.AddresseeId == userBId) ||
            (f.RequesterId == userBId && f.AddresseeId == userAId)));

    public Task<List<Friendship>> GetFriendshipsForUserAsync(long userId, FriendshipStatus? status = null)
    {
        var query = _friendships.Where(f => f.RequesterId == userId || f.AddresseeId == userId);
        if (status.HasValue) query = query.Where(f => f.Status == status.Value);
        return Task.FromResult(query.ToList());
    }

    public Task UpdateFriendshipAsync(Friendship friendship) => Task.CompletedTask;

    public Task DeleteFriendshipAsync(Friendship friendship)
    {
        _friendships.Remove(friendship);
        return Task.CompletedTask;
    }

    public Task DeleteAllFriendshipsForUserAsync(long userId)
    {
        _friendships.RemoveAll(f => f.RequesterId == userId || f.AddresseeId == userId);
        return Task.CompletedTask;
    }
}
