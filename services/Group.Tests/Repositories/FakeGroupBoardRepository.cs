using Group.Entities;
using Group.Repository;

namespace Group.Tests.Repositories;

public sealed class FakeGroupBoardRepository : IGroupBoardRepository
{
    private readonly List<GroupBoard> _boards = [];
    private long _nextId = 1;

    public Task CreateAsync(GroupBoard board)
    {
        typeof(GroupBoard)
            .GetProperty(nameof(GroupBoard.Id))!
            .SetValue(board, _nextId++);
        _boards.Add(board);
        return Task.CompletedTask;
    }

    public Task<GroupBoard?> GetByIdAsync(long id) =>
        Task.FromResult(_boards.FirstOrDefault(b => b.Id == id));

    public Task<GroupBoard?> GetByGroupAndIdAsync(long groupId, long boardId) =>
        Task.FromResult(_boards.FirstOrDefault(b => b.GroupId == groupId && b.Id == boardId));

    public Task<List<GroupBoard>> GetByGroupAndIdsAsync(long groupId, IReadOnlyCollection<long> boardIds) =>
        Task.FromResult(_boards.Where(b => b.GroupId == groupId && boardIds.Contains(b.Id)).ToList());

    public Task<List<GroupBoard>> GetByGroupAsync(long groupId) =>
        Task.FromResult(_boards.Where(b => b.GroupId == groupId).OrderBy(b => b.Name).ToList());

    public Task<bool> ExistsInGroupAsync(long groupId, long boardId) =>
        Task.FromResult(_boards.Any(b => b.GroupId == groupId && b.Id == boardId));

    public Task<bool> ExistsByNameAsync(long groupId, string name, long? excludeBoardId = null) =>
        Task.FromResult(_boards.Any(b =>
            b.GroupId == groupId
            && b.Name == name
            && (excludeBoardId == null || b.Id != excludeBoardId)));

    public Task UpdateAsync(GroupBoard board) => Task.CompletedTask;

    public Task DeleteAsync(GroupBoard board)
    {
        _boards.Remove(board);
        return Task.CompletedTask;
    }

    public Task DeleteAllByGroupAsync(long groupId)
    {
        _boards.RemoveAll(b => b.GroupId == groupId);
        return Task.CompletedTask;
    }
}
