using Social.Client;
using Tangle.AspNetCore.Exceptions;
using Social.Entities;
using Social.Dto;
using Social.Repository;
using Social.Infrastructure;
using Tangle.AspNetCore.Auth;

namespace Social.Service;

[Service]
public class FriendshipService(
    IFriendshipRepository repo,
    IUserClient userClient,
    CurrentUserAccessor currentUser)
{
    private readonly IFriendshipRepository _repo = repo;
    private readonly IUserClient _userClient = userClient;
    private readonly CurrentUserAccessor _currentUser = currentUser;

    private long GetUserIdFromLogin() => _currentUser.GetUserIdFromLogin();

    private async Task<Friendship> GetFriendshipOrThrowAsync(long id) =>
        await _repo.GetFriendshipByIdAsync(id) ?? throw new EntityNotFoundException("Friendship not found");

    public async Task EnsureFriendshipDoesNotExistForUserPairAsync(long userId, long otherUserId, string? message = null)
    {
        if (await _repo.ExistsFriendshipForUserPairAsync(userId, otherUserId))
            throw new EntityAlreadyExistsException(message ?? $"Users {userId} and {otherUserId} are already friends.");
    }

    public async Task EnsureFriendshipExistsForUserPairAsync(
        long userId,
        long otherUserId,
        string message = "You must be friends to open a direct chat.")
    {
        if (await _repo.GetForUserPairAsync(userId, otherUserId) is null) throw new ArgumentException(message);
    }

    public async Task CreateFriendshipForUserPairAsync(long userId, long otherUserId)
    {
        await EnsureFriendshipDoesNotExistForUserPairAsync(userId, otherUserId);
        var friendship = new Friendship(userId, otherUserId);
        await _repo.CreateFriendshipAsync(friendship);
    }

    public async Task DeleteFriendshipByIdAsync(long id)
    {
        var userId = GetUserIdFromLogin();
        var friendship = await GetFriendshipOrThrowAsync(id);
        if (!friendship.Involves(userId)) throw new UnauthorizedAccessException("Unauthorized access");

        await _repo.DeleteFriendshipAsync(friendship);
    }

    public async Task<List<FriendshipGetResponseDto>?> GetMyFriendsAsync()
    {
        var userId = GetUserIdFromLogin();
        var friendships = await _repo.GetAllForUserAsync(userId);
        if (friendships.Count == 0) return null;
        return await MapManyAsync(friendships, userId);
    }

    public async Task<List<FriendshipGetResponseDto>?> GetUserFriendsAsync(long userId)
    {
        var viewerId = GetUserIdFromLogin();
        await _userClient.EnsureUserExistsAsync(userId, "User not found");
        await EnsureCanViewFriendsListAsync(userId, viewerId);

        var friendships = await _repo.GetAllForUserAsync(userId);
        if (friendships.Count == 0) return null;
        return await MapManyAsync(friendships, userId);
    }

    public Task DeleteAllFriendshipsForUserAsync(long userId) => _repo.DeleteAllForUserAsync(userId);

    private async Task EnsureCanViewFriendsListAsync(long targetUserId, long viewerId)
    {
        if (targetUserId == viewerId) return;

        var visibility = await _userClient.GetFriendsListVisibilityAsync(targetUserId);
        switch (visibility)
        {
            case FriendsListVisibility.Public:
                return;
            case FriendsListVisibility.Private:
                throw new UnauthorizedAccessException("This user's friends list is private.");
            case FriendsListVisibility.FriendsOnly:
                if (await _repo.GetForUserPairAsync(targetUserId, viewerId) is null)
                    throw new UnauthorizedAccessException("You must be friends to view this user's friends list.");
                return;
            default:
                throw new ArgumentException("Unknown friends list visibility.", nameof(visibility));
        }
    }

    private static FriendshipGetResponseDto MapToDto(Friendship friendship, long viewerId, string otherUserNickname) =>
        new(
            Id: friendship.Id,
            OtherUserId: friendship.OtherPartyId(viewerId),
            OtherUserNickname: otherUserNickname,
            CreatedAt: friendship.CreatedAt,
            UpdatedAt: friendship.UpdatedAt);

    private async Task<List<FriendshipGetResponseDto>> MapManyAsync(IReadOnlyList<Friendship> friendships, long viewerId)
    {
        var otherIds = friendships.Select(f => f.OtherPartyId(viewerId)).Distinct();
        var nicknames = await _userClient.GetNicknamesByUserIdsAsync(otherIds);

        return [.. friendships.Select(f =>
            MapToDto(f, viewerId, nicknames.GetValueOrDefault(f.OtherPartyId(viewerId), "Deleted User")))];
    }
}
