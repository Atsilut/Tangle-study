using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Repository;

namespace Api.Tests.Repositories;

public sealed class FakeGroupMemberRepository : IGroupMemberRepository
{
    private readonly List<GroupMember> _members = [];
    private long _nextId = 1;

    public Task AddMemberAsync(GroupMember member)
    {
        typeof(GroupMember)
            .GetProperty(nameof(GroupMember.Id))!
            .SetValue(member, _nextId++);
        _members.Add(member);
        return Task.CompletedTask;
    }

    public Task<GroupMember?> GetMemberAsync(long groupId, long userId) =>
        Task.FromResult(_members.FirstOrDefault(m => m.GroupId == groupId && m.UserId == userId));

    public Task<List<GroupMember>> GetMembersByGroupAsync(long groupId) =>
        Task.FromResult(_members.Where(m => m.GroupId == groupId).ToList());

    public Task<IReadOnlyDictionary<long, List<GroupMember>>> GetMembersByGroupIdsAsync(IReadOnlyCollection<long> groupIds) =>
        Task.FromResult<IReadOnlyDictionary<long, List<GroupMember>>>(
            _members
                .Where(m => groupIds.Contains(m.GroupId))
                .GroupBy(m => m.GroupId)
                .ToDictionary(g => g.Key, g => g.ToList()));

    public Task<List<GroupMember>> GetMembershipsByUserAsync(long userId) =>
        Task.FromResult(_members.Where(m => m.UserId == userId).ToList());

    public Task<int> CountMembersAsync(long groupId) =>
        Task.FromResult(_members.Count(m => m.GroupId == groupId));

    public Task UpdateMemberAsync(GroupMember member) => Task.CompletedTask;

    public Task RemoveMemberAsync(GroupMember member)
    {
        _members.Remove(member);
        return Task.CompletedTask;
    }

    public Task RemoveAllByGroupAsync(long groupId)
    {
        _members.RemoveAll(m => m.GroupId == groupId);
        return Task.CompletedTask;
    }

    public Task RemoveAllByUserAsync(long userId)
    {
        _members.RemoveAll(m => m.UserId == userId);
        return Task.CompletedTask;
    }
}
