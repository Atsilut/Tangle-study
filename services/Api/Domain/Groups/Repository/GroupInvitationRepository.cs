using Api.Domain.Groups.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Groups.Repository
{
    [Repository]
    public class GroupInvitationRepository(AppDbContext context) : IGroupInvitationRepository
    {
        private readonly AppDbContext _context = context;

        public Task CreateInvitationAsync(GroupInvitation invitation)
        {
            _context.GroupInvitations.Add(invitation);
            return _context.SaveChangesAsync();
        }

        public Task<GroupInvitation?> GetByIdAsync(long id) => _context.GroupInvitations.FindAsync(id).AsTask();

        public Task<GroupInvitation?> GetForUserAsync(long groupId, long inviteeId) =>
            _context.GroupInvitations.FirstOrDefaultAsync(i =>
                i.GroupId == groupId && i.InviteeId == inviteeId);

        public Task<GroupInvitation?> GetPendingForUserAsync(long groupId, long inviteeId) =>
            _context.GroupInvitations.FirstOrDefaultAsync(i =>
                i.GroupId == groupId && i.InviteeId == inviteeId && i.IsPending);

        public Task<List<GroupInvitation>> GetPendingIncomingForInviteeAsync(long inviteeId) =>
            _context.GroupInvitations
                .Where(i => i.InviteeId == inviteeId && i.IsPending)
                .ToListAsync();

        public Task<List<GroupInvitation>> GetIgnoredOutgoingForInviterAsync(long inviterId) =>
            _context.GroupInvitations
                .Where(i => i.InviterId == inviterId && !i.IsPending)
                .ToListAsync();

        public Task<List<GroupInvitation>> GetIgnoredIncomingForInviteeAsync(long inviteeId) =>
            _context.GroupInvitations
                .Where(i => i.InviteeId == inviteeId && !i.IsPending)
                .ToListAsync();

        public Task UpdateInvitationAsync(GroupInvitation invitation) => _context.SaveChangesAsync();

        public Task DeleteInvitationAsync(GroupInvitation invitation)
        {
            _context.GroupInvitations.Remove(invitation);
            return _context.SaveChangesAsync();
        }

        public async Task DeleteAllForUserAndGroupAsync(long groupId, long userId)
        {
            var invitations = await _context.GroupInvitations
                .Where(i => i.GroupId == groupId && (i.InviteeId == userId || i.InviterId == userId))
                .ToListAsync();
            if (invitations.Count == 0) return;
            _context.GroupInvitations.RemoveRange(invitations);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllByGroupAsync(long groupId)
        {
            var invitations = await _context.GroupInvitations.Where(i => i.GroupId == groupId).ToListAsync();
            if (invitations.Count == 0) return;
            _context.GroupInvitations.RemoveRange(invitations);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllByUserAsync(long userId)
        {
            var invitations = await _context.GroupInvitations
                .Where(i => i.InviterId == userId || i.InviteeId == userId)
                .ToListAsync();
            if (invitations.Count == 0) return;
            _context.GroupInvitations.RemoveRange(invitations);
            await _context.SaveChangesAsync();
        }
    }
}
