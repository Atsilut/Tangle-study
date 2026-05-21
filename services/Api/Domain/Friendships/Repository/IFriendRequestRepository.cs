using Api.Domain.Friendships.Domain;

namespace Api.Domain.Friendships.Repository
{
    public interface IFriendRequestRepository
    {
        public Task CreateAsync(FriendRequest friendRequest);
        public Task<FriendRequest?> GetByIdAsync(long id);
        public Task<FriendRequest?> GetBetweenAsync(long userAId, long userBId);
        public Task<bool> ExistsFriendRequestBetweenAsync(long userAId, long userBId);
        public Task<List<FriendRequest>> GetForUserAsync(long userId, bool? isPending = null);
        public Task UpdateAsync(FriendRequest friendRequest);
        public Task DeleteAsync(FriendRequest friendRequest);
        public Task DeleteAllBetweenAsync(long userAId, long userBId);
        public Task DeleteAllForUserAsync(long userId);
    }
}
