using Api.Domain.Friendships.Domain;

namespace Api.Domain.Friendships.Repository
{
    public interface IFriendshipRepository
    {
        public Task CreateAsync(Friendship friendship);
        public Task<Friendship?> GetByIdAsync(long id);
        public Task<Friendship?> GetBetweenAsync(long userAId, long userBId);
        public Task<List<Friendship>> GetAllForUserAsync(long userId);
        public Task DeleteAsync(Friendship friendship);
        public Task DeleteAllForUserAsync(long userId);
    }
}
