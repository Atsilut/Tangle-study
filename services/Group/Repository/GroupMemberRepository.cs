using Group.Entities;
using Group.Db;
using Group.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Group.Repository
{
    [Repository]
    public class GroupMemberRepository(GroupDbContext context) : IGroupMemberRepository
    {
        private readonly GroupDbContext _context = context;

        public Task AddMemberAsync(GroupMember member)
        {
            _context.GroupMembers.Add(member);
            return _context.SaveChangesAsync();
        }

        public Task<GroupMember?> GetMemberAsync(long groupId, long userId) =>
            _context.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        public Task<List<GroupMember>> GetMembersByUserIdsAsync(long groupId, IReadOnlyCollection<long> userIds)
        {
            if (userIds.Count == 0) return Task.FromResult<List<GroupMember>>([]);
            return _context.GroupMembers
                .Where(m => m.GroupId == groupId && userIds.Contains(m.UserId))
                .ToListAsync();
        }

        public Task<List<GroupMember>> GetMembersByGroupAsync(long groupId) =>
            _context.GroupMembers.Where(m => m.GroupId == groupId).ToListAsync();

        public async Task<IReadOnlyDictionary<long, List<GroupMember>>> GetMembersByGroupIdsAsync(IReadOnlyCollection<long> groupIds)
        {
            if (groupIds.Count == 0) return new Dictionary<long, List<GroupMember>>();

            var members = await _context.GroupMembers
                .Where(m => groupIds.Contains(m.GroupId))
                .ToListAsync();

            return members
                .GroupBy(member => member.GroupId)
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        public Task<List<GroupMember>> GetMembershipsByUserAsync(long userId) =>
            _context.GroupMembers.Where(m => m.UserId == userId).ToListAsync();

        public Task<int> CountMembersAsync(long groupId) =>
            _context.GroupMembers.CountAsync(m => m.GroupId == groupId);

        public async Task<IReadOnlyDictionary<long, int>> GetMemberCountsByGroupIdsAsync(
            IReadOnlyCollection<long> groupIds)
        {
            if (groupIds.Count == 0) return new Dictionary<long, int>();

            var groupIdList = groupIds.Distinct().ToList();
            return await _context.GroupMembers
                .Where(m => groupIdList.Contains(m.GroupId))
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);
        }

        public Task UpdateMemberAsync(GroupMember member) => _context.SaveChangesAsync();

        public Task RemoveMemberAsync(GroupMember member)
        {
            _context.GroupMembers.Remove(member);
            return _context.SaveChangesAsync();
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
