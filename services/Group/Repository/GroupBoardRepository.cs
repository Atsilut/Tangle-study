using Group.Entities;
using Group.Db;
using Group.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Group.Repository
{
    [Repository]
    public class GroupBoardRepository(GroupDbContext context) : IGroupBoardRepository
    {
        private readonly GroupDbContext _context = context;

        public Task CreateAsync(GroupBoard board)
        {
            _context.GroupBoards.Add(board);
            return _context.SaveChangesAsync();
        }

        public Task<GroupBoard?> GetByIdAsync(long id) =>
            _context.GroupBoards.FindAsync(id).AsTask();

        public Task<GroupBoard?> GetByGroupAndIdAsync(long groupId, long boardId) =>
            _context.GroupBoards.FirstOrDefaultAsync(b => b.GroupId == groupId && b.Id == boardId);

        public Task<List<GroupBoard>> GetByGroupAndIdsAsync(long groupId, IReadOnlyCollection<long> boardIds)
        {
            if (boardIds.Count == 0) return Task.FromResult<List<GroupBoard>>([]);

            return _context.GroupBoards
                .Where(b => b.GroupId == groupId && boardIds.Contains(b.Id))
                .ToListAsync();
        }

        public Task<List<GroupBoard>> GetByGroupAsync(long groupId) =>
            _context.GroupBoards
                .Where(b => b.GroupId == groupId)
                .OrderBy(b => b.Name)
                .ToListAsync();

        public Task<bool> ExistsInGroupAsync(long groupId, long boardId) =>
            _context.GroupBoards.AnyAsync(b => b.GroupId == groupId && b.Id == boardId);

        public Task<bool> ExistsByNameAsync(long groupId, string name, long? excludeBoardId = null) =>
            _context.GroupBoards.AnyAsync(b =>
                b.GroupId == groupId
                && b.Name == name
                && (excludeBoardId == null || b.Id != excludeBoardId));

        public Task UpdateAsync(GroupBoard board) => _context.SaveChangesAsync();

        public Task DeleteAsync(GroupBoard board)
        {
            _context.GroupBoards.Remove(board);
            return _context.SaveChangesAsync();
        }

        public Task DeleteAllByGroupAsync(long groupId) =>
            _context.GroupBoards.Where(b => b.GroupId == groupId).ExecuteDeleteAsync();
    }
}
