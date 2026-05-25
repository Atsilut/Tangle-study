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

        public async Task CreateAsync(GroupBlacklist entry)
        {
            _context.GroupBlacklists.Add(entry);
            await _context.SaveChangesAsync();
        }

        public async Task<GroupBlacklist?> GetByIdAsync(long id) => await _context.GroupBlacklists.FindAsync(id);

        public async Task<GroupBlacklist?> GetAsync(long groupId, long userId) =>
            await _context.GroupBlacklists.FirstOrDefaultAsync(b => b.GroupId == groupId && b.UserId == userId);

        public async Task<bool> ExistsAsync(long groupId, long userId) =>
            await _context.GroupBlacklists.AnyAsync(b => b.GroupId == groupId && b.UserId == userId);

        public async Task<List<GroupBlacklist>> GetByGroupAsync(long groupId) =>
            await _context.GroupBlacklists.Where(b => b.GroupId == groupId).ToListAsync();

        public async Task DeleteAsync(GroupBlacklist entry)
        {
            _context.GroupBlacklists.Remove(entry);
            await _context.SaveChangesAsync();
        }
    }
}
