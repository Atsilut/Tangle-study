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
    public class FriendshipService
    {
        private readonly IFriendshipRepository _repo;
        private readonly UserService _userService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FriendshipService(
            IFriendshipRepository repo,
            UserService userService,
            IHttpContextAccessor httpContextAccessor)
        {
            _repo = repo;
            _userService = userService;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        private async Task<Friendship> GetFriendshipOrThrowAsync(long id) =>
            await _repo.GetByIdAsync(id) ?? throw new EntityNotFoundException("Friendship not found");

        public async Task EnsureFriendshipDoesNotExistBetweenAsync(long userAId, long userBId, string? message = null)
        {
            if (await _repo.ExistsFriendshipBetweenAsync(userAId, userBId))
                throw new EntityAlreadyExistsException(message ?? $"Users {userAId} and {userBId} are already friends.");
        }

        public async Task CreateBetweenUsersAsync(long userAId, long userBId)
        {
            await EnsureFriendshipDoesNotExistBetweenAsync(userAId, userBId);
            var friendship = new Friendship(userAId, userBId);
            await _repo.CreateAsync(friendship);
        }

        public async Task<FriendshipResponseDto> MapToResponseDtoAsync(Friendship friendship, long viewerId)
        {
            var otherId = friendship.OtherPartyId(viewerId);
            var other = await _userService.GetUserByIdAsync(otherId);
            return MapToDto(friendship, viewerId, other?.Nickname ?? "Deleted User");
        }

        public async Task DeleteFriendshipByIdAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var friendship = await GetFriendshipOrThrowAsync(id);
            if (!friendship.Involves(userId))
                throw new UnauthorizedAccessException("Unauthorized access");

            await _repo.DeleteAsync(friendship);
        }

        public async Task<List<FriendshipResponseDto>?> GetMyFriendsAsync()
        {
            var userId = GetUserIdFromLogin();
            var friendships = await _repo.GetAllForUserAsync(userId);
            if (friendships.Count == 0) return null;
            return await MapManyAsync(friendships, userId);
        }

        public async Task<List<FriendshipResponseDto>?> GetUserFriendsAsync(long userId)
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
            if (targetUserId == viewerId)
                return;

            var visibility = await _userService.GetFriendsListVisibilityAsync(targetUserId);
            switch (visibility)
            {
                case FriendsListVisibility.Public:
                    return;
                case FriendsListVisibility.Private:
                    throw new UnauthorizedAccessException("This user's friends list is private.");
                case FriendsListVisibility.FriendsOnly:
                    if (await _repo.GetBetweenAsync(targetUserId, viewerId) is null)
                        throw new UnauthorizedAccessException("You must be friends to view this user's friends list.");
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unknown friends list visibility.");
            }
        }

        private static FriendshipResponseDto MapToDto(Friendship friendship, long viewerId, string otherUserNickname) =>
            new(
                Id: friendship.Id,
                OtherUserId: friendship.OtherPartyId(viewerId),
                OtherUserNickname: otherUserNickname,
                CreatedAt: friendship.CreatedAt,
                UpdatedAt: friendship.UpdatedAt);

        private async Task<List<FriendshipResponseDto>> MapManyAsync(IReadOnlyList<Friendship> friendships, long viewerId)
        {
            var otherIds = friendships.Select(f => f.OtherPartyId(viewerId)).Distinct();
            var nicknames = await _userService.GetNicknamesByUserIdsAsync(otherIds);

            return friendships.Select(f =>
                MapToDto(f, viewerId, nicknames.GetValueOrDefault(f.OtherPartyId(viewerId), "Deleted User"))).ToList();
        }
    }
}
