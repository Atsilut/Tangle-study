using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Repository
{
    public interface IGroupBoardRepository
    {
        Task CreateAsync(GroupBoard board);
        Task<GroupBoard?> GetByIdAsync(long id);
        Task<GroupBoard?> GetByGroupAndIdAsync(long groupId, long boardId);
        Task<List<GroupBoard>> GetByGroupAsync(long groupId);
        Task<bool> ExistsInGroupAsync(long groupId, long boardId);
        Task<bool> ExistsByNameAsync(long groupId, string name, long? excludeBoardId = null);
        Task UpdateAsync(GroupBoard board);
        Task DeleteAsync(GroupBoard board);
        Task DeleteAllByGroupAsync(long groupId);
    }
}
