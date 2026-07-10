using Social.Entities;
using Social.Repository;

namespace Social.Tests.Repositories;

public sealed class FakeUserBlockRepository : IUserBlockRepository
{
    private long _nextId = 1;
    public List<UserBlock> Items { get; } = [];

    public Task CreateUserBlockAsync(UserBlock userBlock)
    {
        typeof(UserBlock).GetProperty(nameof(UserBlock.Id))!
            .SetValue(userBlock, _nextId++);
        Items.Add(userBlock);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsUserBlockAsync(long blockerId, long blockedUserId) =>
        Task.FromResult(Items.Any(b => b.BlockerId == blockerId && b.BlockedUserId == blockedUserId));

    public Task<bool> AnyBlockExistsBetweenUserAndOthersAsync(long userId, IReadOnlyCollection<long> otherUserIds)
    {
        if (otherUserIds.Count == 0) return Task.FromResult(false);
        return Task.FromResult(Items.Any(b =>
            (b.BlockerId == userId && otherUserIds.Contains(b.BlockedUserId))
            || (otherUserIds.Contains(b.BlockerId) && b.BlockedUserId == userId)));
    }

    public Task<HashSet<long>> GetMutuallyBlockedUserIdsAsync(long userId, IReadOnlyCollection<long> otherUserIds)
    {
        if (otherUserIds.Count == 0) return Task.FromResult(new HashSet<long>());
        var ids = otherUserIds.ToHashSet();
        var blocked = Items
            .Where(b => (b.BlockerId == userId && ids.Contains(b.BlockedUserId))
                || (ids.Contains(b.BlockerId) && b.BlockedUserId == userId))
            .Select(b => b.BlockerId == userId ? b.BlockedUserId : b.BlockerId)
            .ToHashSet();
        return Task.FromResult(blocked);
    }

    public Task<UserBlock?> GetUserBlockByIdAsync(long id) =>
        Task.FromResult(Items.FirstOrDefault(b => b.Id == id));

    public Task<List<UserBlock>> GetAllForBlockerAsync(long blockerId) =>
        Task.FromResult(Items.Where(b => b.BlockerId == blockerId).OrderByDescending(b => b.CreatedAt).ToList());

    public Task DeleteUserBlockAsync(UserBlock userBlock)
    {
        Items.Remove(userBlock);
        return Task.CompletedTask;
    }

    public Task DeleteAllForUserAsync(long userId)
    {
        Items.RemoveAll(b => b.BlockerId == userId || b.BlockedUserId == userId);
        return Task.CompletedTask;
    }
}
