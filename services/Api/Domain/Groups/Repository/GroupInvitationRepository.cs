using Api.Domain.Groups.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Groups.Repository
{
    [Repository]
    public class GroupInvitationRepository : IGroupInvitationRepository
    {
        private readonly AppDbContext _context;

        public GroupInvitationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateInvitationAsync(GroupInvitation invitation)
        {
            _context.GroupInvitations.Add(invitation);
            await _context.SaveChangesAsync();
        }

        public async Task<GroupInvitation?> GetByIdAsync(long id) => await _context.GroupInvitations.FindAsync(id);

        public async Task<GroupInvitation?> GetPendingForUserAsync(long groupId, long inviteeId) =>
            await _context.GroupInvitations.FirstOrDefaultAsync(i =>
                i.GroupId == groupId && i.InviteeId == inviteeId && i.IsPending);

        public async Task<List<GroupInvitation>> GetPendingForInviteeAsync(long inviteeId) =>
            await _context.GroupInvitations
                .Where(i => i.InviteeId == inviteeId && i.IsPending)
                .ToListAsync();

        public async Task DeleteInvitationAsync(GroupInvitation invitation)
        {
            _context.GroupInvitations.Remove(invitation);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllForUserAndGroupAsync(long groupId, long userId)
        {
            var invitations = await _context.GroupInvitations
                .Where(i => i.GroupId == groupId && i.InviteeId == userId)
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
