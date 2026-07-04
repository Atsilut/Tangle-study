using Group.Entities;
using Group.Repository;
using GroupEntity = Group.Entities.Group;

namespace Group.Tests.Repositories;

public sealed class FakeGroupRepository : IGroupRepository
{
    private readonly List<GroupEntity> _groups = [];
    private long _nextId = 1;

    public Task CreateGroupAsync(GroupEntity group)
    {
        typeof(GroupEntity)
            .GetProperty(nameof(GroupEntity.Id))!
            .SetValue(group, _nextId++);
        _groups.Add(group);
        return Task.CompletedTask;
    }

    public Task<GroupEntity?> GetGroupByIdAsync(long id) =>
        Task.FromResult(_groups.FirstOrDefault(g => g.Id == id));

    public Task<List<GroupEntity>> GetPublicGroupsAsync() =>
        Task.FromResult(_groups.Where(g => g.Visibility == GroupVisibility.Public).ToList());

    public Task<List<GroupEntity>> GetGroupsByIdsAsync(IReadOnlyCollection<long> ids) =>
        Task.FromResult(_groups.Where(g => ids.Contains(g.Id)).ToList());

    public Task<IReadOnlyDictionary<long, string>> GetGroupNamesByIdsAsync(IEnumerable<long> ids) =>
        Task.FromResult<IReadOnlyDictionary<long, string>>(
            _groups
                .Where(g => ids.Contains(g.Id))
                .ToDictionary(g => g.Id, g => g.Name));

    public Task<bool> ExistsGroupByIdAsync(long id) =>
        Task.FromResult(_groups.Any(g => g.Id == id));

    public Task UpdateGroupAsync(GroupEntity group) => Task.CompletedTask;

    public Task DeleteGroupAsync(GroupEntity group)
    {
        _groups.Remove(group);
        return Task.CompletedTask;
    }
}
