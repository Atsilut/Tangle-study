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
        private readonly IFriendRequestRepository _repo;
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
            _repo = requestRepo;
            _friendshipService = friendshipService;
            _userService = userService;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        public async Task<SendFriendRequestOutcome> SendRequestAsync(FriendRequestCreateRequestDto request)
        {
            var requesterId = GetUserIdFromLogin();
            await ValidateSendRequestPartiesAsync(requesterId, request.AddresseeId);

            var existingRequest = await _repo.GetBetweenAsync(requesterId, request.AddresseeId);
            if (existingRequest is not null)
                return await HandleExistingRequestBetweenAsync(requesterId, request.AddresseeId, existingRequest);

            await CreateOutgoingFriendRequestAsync(requesterId, request.AddresseeId);
            return SendFriendRequestOutcome.FriendRequestCreated;
        }

        private async Task ValidateSendRequestPartiesAsync(long requesterId, long addresseeId)
        {
            if (requesterId == addresseeId)
                throw new ArgumentException("Cannot send a friend request to yourself.");

            await _userService.EnsureUserExistsAsync(requesterId, "Authentication failed", StatusCodes.Status400BadRequest);
            await _userService.EnsureUserExistsAsync(addresseeId, "Addressee not found", StatusCodes.Status400BadRequest);
            await _friendshipService.EnsureFriendshipDoesNotExistBetweenAsync(requesterId, addresseeId);
        }

        private async Task<SendFriendRequestOutcome> HandleExistingRequestBetweenAsync(
            long requesterId, long addresseeId, FriendRequest existingRequest)
        {
            if (existingRequest.RequesterId == requesterId)
                throw new EntityAlreadyExistsException($"A request between users {requesterId} and {addresseeId} already exists.");

            await CreateFriendshipFromRequestAsync(requesterId, addresseeId);
            return SendFriendRequestOutcome.FriendshipCreatedFromReciprocalRequest;
        }

        private async Task CreateOutgoingFriendRequestAsync(long requesterId, long addresseeId)
        {
            var friendRequest = new FriendRequest(requesterId, addresseeId);
            await _repo.CreateAsync(friendRequest);
        }

        private Task CreateFriendshipFromRequestAsync(long requesterId, long addresseeId) => 
            _db.ExecuteInTransactionAsync(async () =>
        {
            await _repo.DeleteAllBetweenAsync(requesterId, addresseeId);
            await _friendshipService.CreateBetweenUsersAsync(requesterId, addresseeId);
        });

        public async Task AcceptRequestAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var request = await GetPendingRequestOrThrowAsync(id);
            if (request.AddresseeId != userId)
                throw new UnauthorizedAccessException("Only the addressee can accept a friend request.");

            await CreateFriendshipFromRequestAsync(request.RequesterId, request.AddresseeId);
        }

        private async Task<FriendRequest> GetPendingRequestOrThrowAsync(long id)
        {
            var request = await _repo.GetByIdAsync(id)
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

            await _repo.DeleteAsync(request);
        }

        public async Task<List<FriendRequestResponseDto>?> GetPendingAsync()
        {
            var userId = GetUserIdFromLogin();
            var requests = await _repo.GetForUserAsync(userId, isPending: true);
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
            var request = await _repo.GetByIdAsync(id)
                ?? throw new EntityNotFoundException("Friend request not found");
            if (!request.IsUserInvolved(userId))
                throw new UnauthorizedAccessException("Unauthorized access");

            await _repo.DeleteAsync(request);
        }

        public Task DeleteAllRequestsForUserAsync(long userId) => _repo.DeleteAllForUserAsync(userId);
    }
}
