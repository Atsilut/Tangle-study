using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Repository
{
    public interface IGroupBoardRepository
    {
        public Task CreateAsync(GroupBoard board);
        public Task<GroupBoard?> GetByIdAsync(long id);
        public Task<GroupBoard?> GetByGroupAndIdAsync(long groupId, long boardId);
        public Task<List<GroupBoard>> GetByGroupAndIdsAsync(long groupId, IReadOnlyCollection<long> boardIds);
        public Task<List<GroupBoard>> GetByGroupAsync(long groupId);
        public Task<bool> ExistsInGroupAsync(long groupId, long boardId);
        public Task<bool> ExistsByNameAsync(long groupId, string name, long? excludeBoardId = null);
        public Task UpdateAsync(GroupBoard board);
        public Task DeleteAsync(GroupBoard board);
        public Task DeleteAllByGroupAsync(long groupId);
    }
}
