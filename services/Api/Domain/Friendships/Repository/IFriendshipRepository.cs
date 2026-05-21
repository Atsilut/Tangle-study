using Api.Domain.Friendships.Domain;

namespace Api.Domain.Friendships.Repository
{
    public interface IFriendshipRepository
    {
        public Task CreateFriendshipAsync(Friendship friendship);
        public Task<Friendship?> GetFriendshipByIdAsync(long id);
        public Task<Friendship?> GetFriendshipBetweenAsync(long userAId, long userBId);
        public Task<List<Friendship>> GetFriendshipsForUserAsync(long userId, FriendshipStatus? status = null);
        public Task UpdateFriendshipAsync(Friendship friendship);
        public Task DeleteFriendshipAsync(Friendship friendship);
        public Task DeleteAllFriendshipsForUserAsync(long userId);
    }
}
