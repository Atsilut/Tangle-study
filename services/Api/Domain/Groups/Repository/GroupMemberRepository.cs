using Api.Domain.Groups.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Groups.Repository
{
    [Repository]
    public class GroupMemberRepository : IGroupMemberRepository
    {
        private readonly AppDbContext _context;

        public GroupMemberRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddMemberAsync(GroupMember member)
        {
            _context.GroupMembers.Add(member);
            await _context.SaveChangesAsync();
        }

        public async Task<GroupMember?> GetMemberAsync(long groupId, long userId) =>
            await _context.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        public async Task<List<GroupMember>> GetMembersByGroupAsync(long groupId) =>
            await _context.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync();

        public async Task<List<GroupMember>> GetMembershipsByUserAsync(long userId) =>
            await _context.GroupMembers.Where(m => m.UserId == userId).ToListAsync();

        public async Task<int> CountMembersAsync(long groupId) =>
            await _context.GroupMembers.CountAsync(m => m.GroupId == groupId);

        public async Task UpdateMemberAsync(GroupMember member) => await _context.SaveChangesAsync();

        public async Task RemoveMemberAsync(GroupMember member)
        {
            _context.GroupMembers.Remove(member);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveAllByGroupAsync(long groupId)
        {
            var members = await _context.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync();
            if (members.Count == 0) return;
            _context.GroupMembers.RemoveRange(members);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveAllByUserAsync(long userId)
        {
            var members = await _context.GroupMembers.Where(m => m.UserId == userId).ToListAsync();
            if (members.Count == 0) return;
            _context.GroupMembers.RemoveRange(members);
            await _context.SaveChangesAsync();
        }
    }
}
