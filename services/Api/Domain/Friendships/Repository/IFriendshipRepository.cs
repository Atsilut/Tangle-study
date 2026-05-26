using Api.Domain.Friendships.Domain;

namespace Api.Domain.Friendships.Repository
{
    public interface IFriendshipRepository
    {
        public Task CreateFriendshipAsync(Friendship friendship);
        public Task<Friendship?> GetFriendshipByIdAsync(long id);
        public Task<Friendship?> GetForUserPairAsync(long userId, long otherUserId);
        public Task<bool> ExistsFriendshipForUserPairAsync(long userId, long otherUserId);
        public Task<List<Friendship>> GetAllForUserAsync(long userId);
        public Task DeleteFriendshipAsync(Friendship friendship);
        public Task DeleteAllForUserAsync(long userId);
    }
}
