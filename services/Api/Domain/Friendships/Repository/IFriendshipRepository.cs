using Api.Domain.Friendships.Domain;

namespace Api.Domain.Friendships.Repository
{
    public interface IFriendshipRepository
    {
        Task CreateFriendshipAsync(Friendship friendship);
        Task<Friendship?> GetFriendshipByIdAsync(long id);
        Task<Friendship?> GetFriendshipBetweenAsync(long userAId, long userBId);
        Task<List<Friendship>> GetFriendshipsForUserAsync(long userId, FriendshipStatus? status = null);
        Task UpdateFriendshipAsync(Friendship friendship);
        Task DeleteFriendshipAsync(Friendship friendship);
        Task DeleteAllFriendshipsForUserAsync(long userId);
    }
}
