using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Repository;
using Api.Domain.Users.Domain;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Friendships.Service
{
    [Service]
    public class FriendshipService(
        IFriendshipRepository repo,
        UserService userService,
        IHttpContextAccessor httpContextAccessor)
    {
        private readonly IFriendshipRepository _repo = repo;
        private readonly UserService _userService = userService;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

        private async Task<Friendship> GetFriendshipOrThrowAsync(long id) =>
            await _repo.GetFriendshipByIdAsync(id) ?? throw new EntityNotFoundException("Friendship not found");

        public async Task EnsureFriendshipDoesNotExistForUserPairAsync(long userId, long otherUserId, string? message = null)
        {
            if (await _repo.ExistsFriendshipForUserPairAsync(userId, otherUserId)) throw new EntityAlreadyExistsException(message ?? $"Users {userId} and {otherUserId} are already friends.");
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
            await _userService.EnsureUserExistsAsync(userId, "User not found");
            await EnsureCanViewFriendsListAsync(userId, viewerId);

            var friendships = await _repo.GetAllForUserAsync(userId);
            if (friendships.Count == 0) return null;
            return await MapManyAsync(friendships, userId);
        }

        public Task DeleteAllFriendshipsForUserAsync(long userId) => _repo.DeleteAllForUserAsync(userId);

        private async Task EnsureCanViewFriendsListAsync(long targetUserId, long viewerId)
        {
            if (targetUserId == viewerId) return;

            var visibility = await _userService.GetFriendsListVisibilityAsync(targetUserId);
            switch (visibility)
            {
                case FriendsListVisibility.Public:
                    return;
                case FriendsListVisibility.Private:
                    throw new UnauthorizedAccessException("This user's friends list is private.");
                case FriendsListVisibility.FriendsOnly:
                    if (await _repo.GetForUserPairAsync(targetUserId, viewerId) is null) throw new UnauthorizedAccessException("You must be friends to view this user's friends list.");
                    return;
                default:
                    throw new ArgumentException("Unknown friends list visibility.", nameof(visibility));
            }
        }

        private FriendshipGetResponseDto MapToDto(Friendship friendship, long viewerId, string otherUserNickname) =>
            new(
                Id: friendship.Id,
                OtherUserId: friendship.OtherPartyId(viewerId),
                OtherUserNickname: otherUserNickname,
                CreatedAt: friendship.CreatedAt,
                UpdatedAt: friendship.UpdatedAt);

        private async Task<List<FriendshipGetResponseDto>> MapManyAsync(IReadOnlyList<Friendship> friendships, long viewerId)
        {
            var otherIds = friendships.Select(f => f.OtherPartyId(viewerId)).Distinct();
            var nicknames = await _userService.GetNicknamesByUserIdsAsync(otherIds);

            return [.. friendships.Select(f =>
                MapToDto(f, viewerId, nicknames.GetValueOrDefault(f.OtherPartyId(viewerId), "Deleted User")))];
        }
    }
}
