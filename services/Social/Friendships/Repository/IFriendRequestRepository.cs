using Social.Friendships.Domain;

namespace Social.Friendships.Repository;

public interface IFriendRequestRepository
{
    public Task CreateFriendRequestAsync(FriendRequest friendRequest);
    public Task<FriendRequest?> GetFriendRequestByIdAsync(long id);
    public Task<FriendRequest?> GetForUserPairAsync(long userId, long otherUserId);
    public Task<List<FriendRequest>> GetForUserAsync(long userId, bool? isPending = null);
    public Task UpdateFriendRequestAsync(FriendRequest friendRequest);
    public Task DeleteFriendRequestAsync(FriendRequest friendRequest);
    public Task DeleteAllForUserPairAsync(long userId, long otherUserId);
    public Task DeleteAllForUserAsync(long userId);
}
