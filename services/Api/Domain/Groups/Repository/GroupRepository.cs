using Api.Domain.Groups.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Groups.Repository
{
    [Repository]
    public class GroupRepository(AppDbContext context) : IGroupRepository
    {
        private readonly AppDbContext _context = context;

        public Task CreateGroupAsync(Group group)
        {
            _context.Groups.Add(group);
            return _context.SaveChangesAsync();
        }

        public Task<Group?> GetGroupByIdAsync(long id) => _context.Groups.FindAsync(id).AsTask();

        public Task<bool> ExistsGroupByIdAsync(long id) =>
            _context.Groups.AnyAsync(g => g.Id == id);

        public Task UpdateGroupAsync(Group group) => _context.SaveChangesAsync();

        public Task DeleteGroupAsync(Group group)
        {
            _context.Groups.Remove(group);
            return _context.SaveChangesAsync();
        }
    }
}
