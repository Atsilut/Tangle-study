using Location.Client;
using Tangle.AspNetCore.Exceptions;

namespace Location.Tests.Infrastructure;

public sealed class FakeGroupClient(InMemoryUserClient users) : IGroupClient
{
    private long _nextGroupId = 1;

    public HashSet<long> Groups { get; } = [];
    public HashSet<(long GroupId, long UserId)> GroupMembers { get; } = [];

    public long CreateGroup()
    {
        var id = _nextGroupId++;
        Groups.Add(id);
        return id;
    }

    public void AddGroupMember(long groupId, long userId)
    {
        Groups.Add(groupId);
        users.Users.Add(userId);
        GroupMembers.Add((groupId, userId));
    }

    public void Reset()
    {
        Groups.Clear();
        GroupMembers.Clear();
        _nextGroupId = 1;
    }

    public Task EnsureGroupMemberAsync(
        long groupId,
        long userId,
        string notFoundMessage,
        CancellationToken cancellationToken = default)
    {
        if (!GroupMembers.Contains((groupId, userId)))
            throw new EntityNotFoundException(notFoundMessage);
        return Task.CompletedTask;
    }

    public Task<bool> IsGroupMemberAsync(long groupId, long userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(GroupMembers.Contains((groupId, userId)));

    public Task<IReadOnlyList<GroupMemberSummaryDto>> GetGroupMembersForMemberAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        var members = GroupMembers
            .Where(m => m.GroupId == groupId)
            .Select(m => new GroupMemberSummaryDto(
                m.UserId,
                users.Nicknames.GetValueOrDefault(m.UserId, "Deleted User")))
            .ToList();
        return Task.FromResult<IReadOnlyList<GroupMemberSummaryDto>>(members);
    }

    public Task<IReadOnlyList<long>> GetGroupMemberUserIdsAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        var memberIds = GroupMembers
            .Where(m => m.GroupId == groupId)
            .Select(m => m.UserId)
            .ToList();
        return Task.FromResult<IReadOnlyList<long>>(memberIds);
    }
}
