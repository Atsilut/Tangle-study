using Api.Domain.UserBlocks.Domain;
using Api.Domain.UserBlocks.Repository;

namespace Api.Tests.Repositories;

public sealed class FakeUserBlockRepository : IUserBlockRepository
{
    private readonly List<UserBlock> _blocks = new();
    private long _nextId = 1;

    public Task CreateUserBlockAsync(UserBlock userBlock)
    {
        typeof(UserBlock)
            .GetProperty(nameof(UserBlock.Id))!
            .SetValue(userBlock, _nextId++);
        _blocks.Add(userBlock);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsUserBlockAsync(long blockerId, long blockedUserId) =>
        Task.FromResult(_blocks.Any(b =>
            b.BlockerId == blockerId && b.BlockedUserId == blockedUserId));

    public Task<UserBlock?> GetUserBlockByIdAsync(long id) =>
        Task.FromResult(_blocks.FirstOrDefault(b => b.Id == id));

    public Task<List<UserBlock>> GetAllForBlockerAsync(long blockerId) =>
        Task.FromResult(_blocks
            .Where(b => b.BlockerId == blockerId)
            .OrderByDescending(b => b.CreatedAt)
            .ToList());

    public Task DeleteUserBlockAsync(UserBlock userBlock)
    {
        _blocks.Remove(userBlock);
        return Task.CompletedTask;
    }
}
