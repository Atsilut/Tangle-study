using Group.Entities;

namespace Group.Repository
{
    public interface IGroupBlacklistRepository
    {
        public Task CreateAsync(GroupBlacklist entry);
        public Task<GroupBlacklist?> GetByIdAsync(long id);
        public Task<GroupBlacklist?> GetAsync(long groupId, long userId);
        public Task<bool> ExistsAsync(long groupId, long userId);
        public Task<List<GroupBlacklist>> GetByGroupAsync(long groupId);
        public Task DeleteAsync(GroupBlacklist entry);
        public Task DeleteAllByGroupAsync(long groupId);
    }
}
