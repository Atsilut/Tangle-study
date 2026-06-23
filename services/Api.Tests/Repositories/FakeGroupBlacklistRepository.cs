using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Repository;

namespace Api.Tests.Repositories;

public sealed class FakeGroupBlacklistRepository : IGroupBlacklistRepository
{
    private readonly List<GroupBlacklist> _entries = [];
    private long _nextId = 1;

    public Task CreateAsync(GroupBlacklist entry)
    {
        typeof(GroupBlacklist)
            .GetProperty(nameof(GroupBlacklist.Id))!
            .SetValue(entry, _nextId++);
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<GroupBlacklist?> GetByIdAsync(long id) =>
        Task.FromResult(_entries.FirstOrDefault(e => e.Id == id));

    public Task<GroupBlacklist?> GetAsync(long groupId, long userId) =>
        Task.FromResult(_entries.FirstOrDefault(e => e.GroupId == groupId && e.UserId == userId));

    public Task<bool> ExistsAsync(long groupId, long userId) =>
        Task.FromResult(_entries.Any(e => e.GroupId == groupId && e.UserId == userId));

    public Task<List<GroupBlacklist>> GetByGroupAsync(long groupId) =>
        Task.FromResult(_entries.Where(e => e.GroupId == groupId).ToList());

    public Task DeleteAsync(GroupBlacklist entry)
    {
        _entries.Remove(entry);
        return Task.CompletedTask;
    }

    public Task DeleteAllByGroupAsync(long groupId)
    {
        _entries.RemoveAll(e => e.GroupId == groupId);
        return Task.CompletedTask;
    }
}
