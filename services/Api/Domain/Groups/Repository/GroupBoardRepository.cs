using Api.Domain.Groups.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Groups.Repository
{
    [Repository]
    public class GroupBoardRepository : IGroupBoardRepository
    {
        private readonly AppDbContext _context;

        public GroupBoardRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(GroupBoard board)
        {
            _context.GroupBoards.Add(board);
            await _context.SaveChangesAsync();
        }

        public async Task<GroupBoard?> GetByIdAsync(long id) =>
            await _context.GroupBoards.FindAsync(id);

        public async Task<GroupBoard?> GetByGroupAndIdAsync(long groupId, long boardId) =>
            await _context.GroupBoards.FirstOrDefaultAsync(b => b.GroupId == groupId && b.Id == boardId);

        public async Task<List<GroupBoard>> GetByGroupAsync(long groupId) =>
            await _context.GroupBoards
                .Where(b => b.GroupId == groupId)
                .OrderBy(b => b.Name)
                .ToListAsync();

        public async Task<bool> ExistsInGroupAsync(long groupId, long boardId) =>
            await _context.GroupBoards.AnyAsync(b => b.GroupId == groupId && b.Id == boardId);

        public async Task<bool> ExistsByNameAsync(long groupId, string name, long? excludeBoardId = null) =>
            await _context.GroupBoards.AnyAsync(b =>
                b.GroupId == groupId
                && b.Name == name
                && (excludeBoardId == null || b.Id != excludeBoardId));

        public async Task UpdateAsync(GroupBoard board) => await _context.SaveChangesAsync();

        public async Task DeleteAsync(GroupBoard board)
        {
            _context.GroupBoards.Remove(board);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAllByGroupAsync(long groupId) =>
            await _context.GroupBoards.Where(b => b.GroupId == groupId).ExecuteDeleteAsync();
    }
}
