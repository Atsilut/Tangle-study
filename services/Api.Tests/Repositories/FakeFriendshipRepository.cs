using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Repository;

namespace Api.Tests.Repositories;

public sealed class FakeFriendshipRepository : IFriendshipRepository
{
    private readonly List<Friendship> _friendships = [];
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

    public Task<Friendship?> GetForUserPairAsync(long userId, long otherUserId)
    {
        var userLowId = Math.Min(userId, otherUserId);
        var userHighId = Math.Max(userId, otherUserId);
        return Task.FromResult(_friendships.FirstOrDefault(f =>
            f.UserLowId == userLowId && f.UserHighId == userHighId));
    }

    public Task<bool> ExistsFriendshipForUserPairAsync(long userId, long otherUserId)
    {
        var userLowId = Math.Min(userId, otherUserId);
        var userHighId = Math.Max(userId, otherUserId);
        return Task.FromResult(_friendships.Any(f =>
            f.UserLowId == userLowId && f.UserHighId == userHighId));
    }

    public Task<List<Friendship>> GetAllForUserAsync(long userId) =>
        Task.FromResult(_friendships.Where(f => f.UserLowId == userId || f.UserHighId == userId).ToList());

    public Task DeleteFriendshipAsync(Friendship friendship)
    {
        _friendships.Remove(friendship);
        return Task.CompletedTask;
    }

    public Task DeleteAllForUserAsync(long userId)
    {
        _friendships.RemoveAll(f => f.UserLowId == userId || f.UserHighId == userId);
        return Task.CompletedTask;
    }
}
