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

        public Task<List<Group>> GetPublicGroupsAsync() =>
            _context.Groups
                .Where(g => g.Visibility == GroupVisibility.Public)
                .OrderBy(g => g.Name)
                .ToListAsync();

        public async Task<List<Group>> GetGroupsByIdsAsync(IReadOnlyCollection<long> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return [];

            var groups = await _context.Groups
                .Where(g => idList.Contains(g.Id))
                .ToListAsync();

            return [.. groups.OrderBy(g => g.Name)];
        }

        public Task<IReadOnlyDictionary<long, string>> GetGroupNamesByIdsAsync(IEnumerable<long> ids)
        {
            List<long> idList = [.. ids.Distinct()];
            if (idList.Count == 0)
            {
                Dictionary<long, string> empty = [];
                return Task.FromResult<IReadOnlyDictionary<long, string>>(empty);
            }

            return QueryGroupNamesByIdsAsync(idList);
        }

        private async Task<IReadOnlyDictionary<long, string>> QueryGroupNamesByIdsAsync(List<long> idList) =>
            await _context.Groups
                .Where(g => idList.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name);

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
