using Api.Domain.UserBlocks.Domain;

namespace Api.Domain.UserBlocks.Repository
{
    public interface IUserBlockRepository
    {
        Task CreateAsync(UserBlock userBlock);
        Task<bool> ExistsAsync(long blockerId, long blockedUserId);
        Task<UserBlock?> GetByIdAsync(long id);
        Task<List<UserBlock>> GetAllForBlockerAsync(long blockerId);
        Task DeleteAsync(UserBlock userBlock);
    }
}
