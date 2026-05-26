using Api.Domain.Groups.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Groups.Repository
{
    [Repository]
    public class GroupRepository : IGroupRepository
    {
        private readonly AppDbContext _context;

        public GroupRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateGroupAsync(Group group)
        {
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();
        }

        public async Task<Group?> GetGroupByIdAsync(long id) => await _context.Groups.FindAsync(id);

        public async Task<bool> ExistsGroupByIdAsync(long id) =>
            await _context.Groups.AnyAsync(g => g.Id == id);

        public async Task UpdateGroupAsync(Group group) => await _context.SaveChangesAsync();

        public async Task DeleteGroupAsync(Group group)
        {
            _context.Groups.Remove(group);
            await _context.SaveChangesAsync();
        }
    }
}
