using Api.Domain.Groups.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Groups.Repository
{
    [Repository]
    public class GroupBlacklistRepository : IGroupBlacklistRepository
    {
        private readonly AppDbContext _context;

        public GroupBlacklistRepository(AppDbContext context)
        {
            _context = context;
        }

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
