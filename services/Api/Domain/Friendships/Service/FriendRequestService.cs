using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Repository;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Friendships.Service
{
    [Service]
    public class FriendRequestService
    {
        private readonly IFriendRequestRepository _requestRepo;
        private readonly FriendshipService _friendshipService;
        private readonly UserService _userService;
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public FriendRequestService(
            IFriendRequestRepository requestRepo,
            FriendshipService friendshipService,
            UserService userService,
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor)
        {
            _requestRepo = requestRepo;
            _friendshipService = friendshipService;
            _userService = userService;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        public async Task SendRequestAsync(FriendRequestCreateRequestDto request)
        {
            var requesterId = GetUserIdFromLogin();
            if (requesterId == request.AddresseeId)
                throw new ArgumentException("Cannot send a friend request to yourself.");

            await _userService.EnsureUserExistsAsync(requesterId, "Authentication failed", StatusCodes.Status400BadRequest);
            await _userService.EnsureUserExistsAsync(request.AddresseeId, "Addressee not found", StatusCodes.Status400BadRequest);

            await _friendshipService.EnsureFriendshipDoesNotExistBetweenAsync(requesterId, request.AddresseeId);
            await EnsureFriendRequestDoesNotExistBetweenAsync(requesterId, request.AddresseeId);

            var friendRequest = new FriendRequest(requesterId, request.AddresseeId);
            await _requestRepo.CreateAsync(friendRequest);
        }

        private async Task EnsureFriendRequestDoesNotExistBetweenAsync(long userAId, long userBId, string? message = null)
        {
            if (await _requestRepo.ExistsFriendRequestBetweenAsync(userAId, userBId))
                throw new EntityAlreadyExistsException(message ?? $"A request between users {userAId} and {userBId} already exists.");
        }

        public async Task AcceptRequestAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var request = await GetPendingRequestOrThrowAsync(id);
            if (request.AddresseeId != userId)
                throw new UnauthorizedAccessException("Only the addressee can accept a friend request.");

            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _requestRepo.DeleteAllBetweenAsync(request.RequesterId, request.AddresseeId);
                await _friendshipService.CreateBetweenUsersAsync(request.RequesterId, request.AddresseeId);
            });
        }

        private async Task<FriendRequest> GetPendingRequestOrThrowAsync(long id)
        {
            var request = await _requestRepo.GetByIdAsync(id)
                ?? throw new EntityNotFoundException("Friend request not found");
            if (!request.IsPending)
                throw new ArgumentException("Invalid Friend Request.");
            return request;
        }

        public async Task RejectRequestAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var request = await GetPendingRequestOrThrowAsync(id);
            if (request.AddresseeId != userId)
                throw new UnauthorizedAccessException("Only the addressee can reject a friend request.");

            await _requestRepo.DeleteAsync(request);
        }

        public async Task<List<FriendRequestResponseDto>?> GetPendingAsync()
        {
            var userId = GetUserIdFromLogin();
            var requests = await _requestRepo.GetForUserAsync(userId, isPending: true);
            if (requests.Count == 0) return null;
            return await MapRequestsAsync(requests, userId);
        }

        private async Task<List<FriendRequestResponseDto>> MapRequestsAsync(IReadOnlyList<FriendRequest> requests, long viewerId)
        {
            var otherIds = requests.Select(r => r.OtherPartyId(viewerId)).Distinct();
            var nicknames = await _userService.GetNicknamesByUserIdsAsync(otherIds);

            return requests.Select(r =>
                MapRequestToDto(r, viewerId, nicknames.GetValueOrDefault(r.OtherPartyId(viewerId), "Deleted User"))).ToList();
        }

        private static FriendRequestResponseDto MapRequestToDto(FriendRequest request, long viewerId, string otherUserNickname) =>
            new(
                Id: request.Id,
                RequesterId: request.RequesterId,
                AddresseeId: request.AddresseeId,
                OtherUserId: request.OtherPartyId(viewerId),
                OtherUserNickname: otherUserNickname,
                IsPending: request.IsPending,
                IsIncoming: request.AddresseeId == viewerId,
                CreatedAt: request.CreatedAt,
                UpdatedAt: request.UpdatedAt);

        public async Task DeleteRequestByIdAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var request = await _requestRepo.GetByIdAsync(id)
                ?? throw new EntityNotFoundException("Friend request not found");
            if (!request.IsUserInvolved(userId))
                throw new UnauthorizedAccessException("Unauthorized access");

            await _requestRepo.DeleteAsync(request);
        }

        public Task DeleteAllRequestsForUserAsync(long userId) => _requestRepo.DeleteAllForUserAsync(userId);
    }
}
