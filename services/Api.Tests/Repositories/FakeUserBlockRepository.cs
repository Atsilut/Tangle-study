using Api.Domain.UserBlocks.Domain;
using Api.Domain.UserBlocks.Repository;

namespace Api.Tests.Repositories;

public sealed class FakeUserBlockRepository : IUserBlockRepository
{
    private readonly List<UserBlock> _blocks = new();
    private long _nextId = 1;

    public Task CreateAsync(UserBlock userBlock)
    {
        typeof(UserBlock)
            .GetProperty(nameof(UserBlock.Id))!
            .SetValue(userBlock, _nextId++);
        _blocks.Add(userBlock);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(long blockerId, long blockedUserId) =>
        Task.FromResult(_blocks.Any(b =>
            b.BlockerId == blockerId && b.BlockedUserId == blockedUserId));
}
