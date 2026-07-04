using Social.UserBlocks.Domain;

namespace Social.UserBlocks.Repository;

public interface IUserBlockRepository
{
    public Task CreateUserBlockAsync(UserBlock userBlock);
    public Task<bool> ExistsUserBlockAsync(long blockerId, long blockedUserId);
    public Task<bool> AnyBlockExistsBetweenUserAndOthersAsync(long userId, IReadOnlyCollection<long> otherUserIds);
    public Task<HashSet<long>> GetMutuallyBlockedUserIdsAsync(long userId, IReadOnlyCollection<long> otherUserIds);
    public Task<UserBlock?> GetUserBlockByIdAsync(long id);
    public Task<List<UserBlock>> GetAllForBlockerAsync(long blockerId);
    public Task DeleteUserBlockAsync(UserBlock userBlock);
    public Task DeleteAllForUserAsync(long userId);
}
