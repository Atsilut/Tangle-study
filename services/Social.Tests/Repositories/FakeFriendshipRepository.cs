using Social.Friendships.Domain;
using Social.Friendships.Repository;

namespace Social.Tests.Repositories;

public sealed class FakeFriendshipRepository : IFriendshipRepository
{
    private long _nextId = 1;
    public List<Friendship> Items { get; } = [];

    public Task CreateFriendshipAsync(Friendship friendship)
    {
        typeof(Friendship).GetProperty(nameof(Friendship.Id))!
            .SetValue(friendship, _nextId++);
        Items.Add(friendship);
        return Task.CompletedTask;
    }

    public Task<Friendship?> GetFriendshipByIdAsync(long id) =>
        Task.FromResult(Items.FirstOrDefault(f => f.Id == id));

    public Task<Friendship?> GetForUserPairAsync(long userId, long otherUserId)
    {
        var low = Math.Min(userId, otherUserId);
        var high = Math.Max(userId, otherUserId);
        return Task.FromResult(Items.FirstOrDefault(f => f.UserLowId == low && f.UserHighId == high));
    }

    public async Task<bool> ExistsFriendshipForUserPairAsync(long userId, long otherUserId) =>
        await GetForUserPairAsync(userId, otherUserId) is not null;

    public Task<List<Friendship>> GetAllForUserAsync(long userId) =>
        Task.FromResult(Items.Where(f => f.UserLowId == userId || f.UserHighId == userId).ToList());

    public Task DeleteFriendshipAsync(Friendship friendship)
    {
        Items.Remove(friendship);
        return Task.CompletedTask;
    }

    public Task DeleteAllForUserAsync(long userId)
    {
        Items.RemoveAll(f => f.UserLowId == userId || f.UserHighId == userId);
        return Task.CompletedTask;
    }
}
