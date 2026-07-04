using Group.Entities;
using Group.Db;
using Group.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Group.Repository
{
    [Repository]
    public class GroupBlacklistRepository(GroupDbContext context) : IGroupBlacklistRepository
    {
        private readonly GroupDbContext _context = context;

        public Task CreateAsync(GroupBlacklist entry)
        {
            _context.GroupBlacklists.Add(entry);
            return _context.SaveChangesAsync();
        }

        public Task<GroupBlacklist?> GetByIdAsync(long id) => _context.GroupBlacklists.FindAsync(id).AsTask();

        public Task<GroupBlacklist?> GetAsync(long groupId, long userId) =>
            _context.GroupBlacklists.FirstOrDefaultAsync(b => b.GroupId == groupId && b.UserId == userId);

        public Task<bool> ExistsAsync(long groupId, long userId) =>
            _context.GroupBlacklists.AnyAsync(b => b.GroupId == groupId && b.UserId == userId);

        public Task<List<GroupBlacklist>> GetByGroupAsync(long groupId) =>
            _context.GroupBlacklists.Where(b => b.GroupId == groupId).ToListAsync();

        public Task DeleteAsync(GroupBlacklist entry)
        {
            _context.GroupBlacklists.Remove(entry);
            return _context.SaveChangesAsync();
        }

        public Task DeleteAllByGroupAsync(long groupId) =>
            _context.GroupBlacklists.Where(b => b.GroupId == groupId).ExecuteDeleteAsync();
    }
}
