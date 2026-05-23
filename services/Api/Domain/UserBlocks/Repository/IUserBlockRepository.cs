using Api.Domain.UserBlocks.Domain;

namespace Api.Domain.UserBlocks.Repository
{
    public interface IUserBlockRepository
    {
        public Task CreateAsync(UserBlock userBlock);
        public Task<bool> ExistsAsync(long blockerId, long blockedUserId);
        public Task<UserBlock?> GetByIdAsync(long id);
        public Task<List<UserBlock>> GetAllForBlockerAsync(long blockerId);
        public Task DeleteAsync(UserBlock userBlock);
    }
}
