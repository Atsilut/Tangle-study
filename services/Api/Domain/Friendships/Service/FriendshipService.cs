using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Repository;
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
            ?? throw new EntityNotFoundException("Unauthorized access"));

        private async Task<Friendship> GetFriendshipOrThrowAsync(long id) => await _repo.GetFriendshipByIdAsync(id) 
            ?? throw new EntityNotFoundException("Friendship not found");
       
        public async Task<FriendshipRequestResponseDto> SendRequestAsync(FriendshipRequestCreateRequestDto request)
        {
            var requesterId = GetUserIdFromLogin();
            if (requesterId == request.AddresseeId)
                throw new ArgumentException("Cannot send a friend request to yourself.");

            await _userService.EnsureUserExistsAsync(requesterId, "Authentication failed", StatusCodes.Status400BadRequest);
            await _userService.EnsureUserExistsAsync(request.AddresseeId, "Addressee not found", StatusCodes.Status400BadRequest);

            var existing = await _repo.GetFriendshipBetweenAsync(requesterId, request.AddresseeId);
            if (existing is not null)
                throw new EntityAlreadyExistsException($"A request between users {requesterId} and {request.AddresseeId} already exists.");

            var friendship = new Friendship(requesterId, request.AddresseeId);
            await _repo.CreateFriendshipAsync(friendship);
            return await MapToDtoAsync(friendship, requesterId);
        }

        public async Task<FriendshipRequestResponseDto> AcceptRequestAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var friendship = await GetFriendshipOrThrowAsync(id);
            if (friendship.AddresseeId != userId)
                throw new UnauthorizedAccessException("Only the addressee can accept a friend request.");

            friendship.Accept();
            await _repo.UpdateFriendshipAsync(friendship);
            return await MapToDtoAsync(friendship, userId);
        }

        public async Task<FriendshipRequestResponseDto> RejectRequestAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var friendship = await GetFriendshipOrThrowAsync(id);
            if (friendship.AddresseeId != userId)
                throw new UnauthorizedAccessException("Only the addressee can reject a friend request.");

            friendship.Reject();
            await _repo.UpdateFriendshipAsync(friendship);
            return await MapToDtoAsync(friendship, userId);
        }

        public async Task CancelRequestAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var friendship = await GetFriendshipOrThrowAsync(id);
            if (!friendship.Involves(userId))
                throw new UnauthorizedAccessException("Unauthorized access");

            await _repo.DeleteFriendshipAsync(friendship);
        }

        public async Task<List<FriendshipRequestResponseDto>> GetMyFriendsAsync()
        {
            var userId = GetUserIdFromLogin();
            var friendships = await _repo.GetFriendshipsForUserAsync(userId, FriendshipStatus.Accepted);
            return await MapManyAsync(friendships, userId);
        }

        public async Task<List<FriendshipRequestResponseDto>> GetPendingAsync()
        {
            var userId = GetUserIdFromLogin();
            var friendships = await _repo.GetFriendshipsForUserAsync(userId, FriendshipStatus.Pending);
            return await MapManyAsync(friendships, userId);
        }

        public Task DeleteAllForUserAsync(long userId) => _repo.DeleteAllFriendshipsForUserAsync(userId);

        private async Task<FriendshipRequestResponseDto> MapToDtoAsync(Friendship friendship, long viewerId)
        {
            var otherId = friendship.OtherPartyId(viewerId);
            var other = await _userService.GetUserByIdAsync(otherId);
            return new FriendshipRequestResponseDto(
                Id: friendship.Id,
                RequesterId: friendship.RequesterId,
                AddresseeId: friendship.AddresseeId,
                OtherUserId: otherId,
                OtherUserNickname: other?.Nickname ?? "Deleted User",
                Status: friendship.Status,
                IsIncoming: friendship.AddresseeId == viewerId,
                CreatedAt: friendship.CreatedAt,
                UpdatedAt: friendship.UpdatedAt);
        }

        private async Task<List<FriendshipRequestResponseDto>> MapManyAsync(IReadOnlyList<Friendship> friendships, long viewerId)
        {
            if (friendships.Count == 0) return new List<FriendshipRequestResponseDto>();

            var otherIds = friendships.Select(f => f.OtherPartyId(viewerId)).Distinct();
            var nicknames = await _userService.GetNicknamesByUserIdsAsync(otherIds);

            return friendships.Select(f =>
            {
                var otherId = f.OtherPartyId(viewerId);
                return new FriendshipRequestResponseDto(
                    Id: f.Id,
                    RequesterId: f.RequesterId,
                    AddresseeId: f.AddresseeId,
                    OtherUserId: otherId,
                    OtherUserNickname: nicknames.GetValueOrDefault(otherId, "Deleted User"),
                    Status: f.Status,
                    IsIncoming: f.AddresseeId == viewerId,
                    CreatedAt: f.CreatedAt,
                    UpdatedAt: f.UpdatedAt);
            }).ToList();
        }
    }
}
