using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Repository
{
    public interface IGroupBlacklistRepository
    {
        Task CreateAsync(GroupBlacklist entry);
        Task<GroupBlacklist?> GetByIdAsync(long id);
        Task<GroupBlacklist?> GetAsync(long groupId, long userId);
        Task<bool> ExistsAsync(long groupId, long userId);
        Task<List<GroupBlacklist>> GetByGroupAsync(long groupId);
        Task DeleteAsync(GroupBlacklist entry);
    }
}
