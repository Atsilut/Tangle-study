using Api.Domain.UserBlocks.Domain;

namespace Api.Domain.UserBlocks.Repository
{
    public interface IUserBlockRepository
    {
        Task CreateAsync(UserBlock userBlock);
        Task<bool> ExistsAsync(long blockerId, long blockedUserId);
    }
}
