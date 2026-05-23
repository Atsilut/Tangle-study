using Api.Domain.Friendships.Domain;

namespace Api.Domain.Friendships.Repository
{
    public interface IFriendRequestRepository
    {
        public Task CreateAsync(FriendRequest friendRequest);
        public Task<FriendRequest?> GetByIdAsync(long id);
        public Task<FriendRequest?> GetForUserPairAsync(long userId, long otherUserId);
        public Task<List<FriendRequest>> GetForUserAsync(long userId, bool? isPending = null);
        public Task UpdateAsync(FriendRequest friendRequest);
        public Task DeleteAsync(FriendRequest friendRequest);
        public Task DeleteAllForUserPairAsync(long userId, long otherUserId);
    }
}
