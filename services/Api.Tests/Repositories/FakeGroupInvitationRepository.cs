using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Repository;

namespace Api.Tests.Repositories;

public sealed class FakeGroupInvitationRepository : IGroupInvitationRepository
{
    private readonly List<GroupInvitation> _invitations = new();
    private long _nextId = 1;

    public Task CreateInvitationAsync(GroupInvitation invitation)
    {
        typeof(GroupInvitation)
            .GetProperty(nameof(GroupInvitation.Id))!
            .SetValue(invitation, _nextId++);
        _invitations.Add(invitation);
        return Task.CompletedTask;
    }

    public Task<GroupInvitation?> GetByIdAsync(long id) =>
        Task.FromResult(_invitations.FirstOrDefault(i => i.Id == id));

    public Task<GroupInvitation?> GetForUserAsync(long groupId, long inviteeId) =>
        Task.FromResult(_invitations.FirstOrDefault(i =>
            i.GroupId == groupId && i.InviteeId == inviteeId));

    public Task<GroupInvitation?> GetPendingForUserAsync(long groupId, long inviteeId) =>
        Task.FromResult(_invitations.FirstOrDefault(i =>
            i.GroupId == groupId && i.InviteeId == inviteeId && i.IsPending));

    public Task<List<GroupInvitation>> GetPendingIncomingForInviteeAsync(long inviteeId) =>
        Task.FromResult(_invitations.Where(i => i.InviteeId == inviteeId && i.IsPending).ToList());

    public Task<List<GroupInvitation>> GetIgnoredOutgoingForInviterAsync(long inviterId) =>
        Task.FromResult(_invitations.Where(i => i.InviterId == inviterId && !i.IsPending).ToList());

    public Task<List<GroupInvitation>> GetIgnoredIncomingForInviteeAsync(long inviteeId) =>
        Task.FromResult(_invitations.Where(i => i.InviteeId == inviteeId && !i.IsPending).ToList());

    public Task UpdateInvitationAsync(GroupInvitation invitation) => Task.CompletedTask;

    public Task DeleteInvitationAsync(GroupInvitation invitation)
    {
        _invitations.Remove(invitation);
        return Task.CompletedTask;
    }

    public Task DeleteAllForUserAndGroupAsync(long groupId, long userId)
    {
        _invitations.RemoveAll(i => i.GroupId == groupId && (i.InviteeId == userId || i.InviterId == userId));
        return Task.CompletedTask;
    }

    public Task DeleteAllByGroupAsync(long groupId)
    {
        _invitations.RemoveAll(i => i.GroupId == groupId);
        return Task.CompletedTask;
    }

    public Task DeleteAllByUserAsync(long userId)
    {
        _invitations.RemoveAll(i => i.InviterId == userId || i.InviteeId == userId);
        return Task.CompletedTask;
    }
}
