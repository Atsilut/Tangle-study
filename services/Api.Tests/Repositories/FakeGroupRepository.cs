using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Repository;

namespace Api.Tests.Repositories;

public sealed class FakeGroupRepository : IGroupRepository
{
    private readonly List<Group> _groups = [];
    private long _nextId = 1;

    public Task CreateGroupAsync(Group group)
    {
        typeof(Group)
            .GetProperty(nameof(Group.Id))!
            .SetValue(group, _nextId++);
        _groups.Add(group);
        return Task.CompletedTask;
    }

    public Task<Group?> GetGroupByIdAsync(long id) =>
        Task.FromResult(_groups.FirstOrDefault(g => g.Id == id));

    public Task<IReadOnlyDictionary<long, string>> GetGroupNamesByIdsAsync(IEnumerable<long> ids) =>
        Task.FromResult<IReadOnlyDictionary<long, string>>(
            _groups
                .Where(g => ids.Contains(g.Id))
                .ToDictionary(g => g.Id, g => g.Name));

    public Task<bool> ExistsGroupByIdAsync(long id) =>
        Task.FromResult(_groups.Any(g => g.Id == id));

    public Task UpdateGroupAsync(Group group) => Task.CompletedTask;

    public Task DeleteGroupAsync(Group group)
    {
        _groups.Remove(group);
        return Task.CompletedTask;
    }
}
