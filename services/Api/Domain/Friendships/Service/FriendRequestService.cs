using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Repository;
using Api.Domain.UserBlocks.Service;
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
        private readonly UserBlockService _userBlockService;
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<FriendRequestService> _logger;

        public FriendRequestService(
            IFriendRequestRepository requestRepo,
            FriendshipService friendshipService,
            UserService userService,
            UserBlockService userBlockService,
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor,
            ILogger<FriendRequestService> logger)
        {
            _repo = requestRepo;
            _friendshipService = friendshipService;
            _userService = userService;
            _userBlockService = userBlockService;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        public async Task<SendFriendRequestOutcome> SendRequestAsync(FriendRequestCreateRequestDto request)
        {
            var requesterId = GetUserIdFromLogin();
            await ValidateSendRequestPartiesAsync(requesterId, request.AddresseeId);

            var existingRequest = await _repo.GetForUserPairAsync(requesterId, request.AddresseeId);
            if (existingRequest is not null)
                return await HandleExistingFriendRequestAsync(requesterId, request.AddresseeId, existingRequest);

            await CreateOutgoingFriendRequestAsync(requesterId, request.AddresseeId);
            return SendFriendRequestOutcome.FriendRequestCreated;
        }

        private async Task ValidateSendRequestPartiesAsync(long requesterId, long addresseeId)
        {
            if (requesterId == addresseeId)
                throw new ArgumentException("Cannot send a friend request to yourself.");

            await _userService.EnsureUserExistsAsync(requesterId, "Authentication failed", StatusCodes.Status400BadRequest);
            await _userService.EnsureUserExistsAsync(addresseeId, "Addressee not found", StatusCodes.Status400BadRequest);
            await _friendshipService.EnsureFriendshipDoesNotExistForUserPairAsync(requesterId, addresseeId);

            if (await _userBlockService.IsBlockedByAsync(requesterId, addresseeId))
                throw new ArgumentException("Cannot send a friend request to a user you have blocked.");
        }

        private Task<bool> IsAddresseeBlockingRequesterAsync(long addresseeId, long requesterId) =>
            _userBlockService.IsBlockedByAsync(addresseeId, requesterId);

        private async Task EnsureNoBlockExistsBetweenUsersAsync(long userId, long otherUserId)
        {
            if (await _userBlockService.IsBlockedByAsync(userId, otherUserId)
                || await _userBlockService.IsBlockedByAsync(otherUserId, userId))
                throw new ArgumentException("Cannot form a friendship while a block exists between you and this user.");
        }

        private async Task<SendFriendRequestOutcome> HandleExistingFriendRequestAsync(
            long requesterId, long addresseeId, FriendRequest existingRequest)
        {
            if (existingRequest.RequesterId == requesterId)
            {
                if (!existingRequest.IsPending)
                {
                    if (!await IsAddresseeBlockingRequesterAsync(addresseeId, requesterId))
                    {
                        existingRequest.Unignore();
                        await _repo.UpdateAsync(existingRequest);
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Friend request for user pair {RequesterId}, {AddresseeId} already exists and is pending.",
                        requesterId,
                        addresseeId);
                }

                return SendFriendRequestOutcome.FriendRequestCreated;
            }

            if (await IsAddresseeBlockingRequesterAsync(addresseeId, requesterId)
                || await _userBlockService.IsBlockedByAsync(requesterId, addresseeId))
                return SendFriendRequestOutcome.FriendRequestCreated;

            await CreateFriendshipFromRequestAsync(requesterId, addresseeId);
            return SendFriendRequestOutcome.FriendshipCreatedFromReciprocalRequest;
        }

        public async Task IgnorePendingRequestForUserPairAsync(long userId, long otherUserId)
        {
            var friendRequest = await _repo.GetForUserPairAsync(userId, otherUserId);
            if (friendRequest is null || !friendRequest.IsPending) return;
            friendRequest.Ignore();
            await _repo.UpdateAsync(friendRequest);
        }

        private async Task CreateOutgoingFriendRequestAsync(long requesterId, long addresseeId)
        {
            var friendRequest = new FriendRequest(requesterId, addresseeId);
            if (await IsAddresseeBlockingRequesterAsync(addresseeId, requesterId))
                friendRequest.Ignore();
            await _repo.CreateAsync(friendRequest);
        }

        private async Task CreateFriendshipFromRequestAsync(long requesterId, long addresseeId)
        {
            await EnsureNoBlockExistsBetweenUsersAsync(requesterId, addresseeId);
            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _repo.DeleteAllForUserPairAsync(requesterId, addresseeId);
                await _friendshipService.CreateFriendshipForUserPairAsync(requesterId, addresseeId);
            });
        }

        public async Task IgnoreRequestAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var request = await GetIncomingRequestForAddresseeOrThrowAsync(id, userId, requirePending: true);
            request.Ignore();
            await _repo.UpdateAsync(request);
        }

        public async Task AcceptRequestAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var request = await GetIncomingRequestForAddresseeOrThrowAsync(id, userId, requirePending: false);
            await CreateFriendshipFromRequestAsync(request.RequesterId, request.AddresseeId);
        }

        public async Task RejectRequestAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var request = await GetIncomingRequestForAddresseeOrThrowAsync(id, userId, requirePending: false);
            await _repo.DeleteAsync(request);
        }

        private async Task<FriendRequest> GetIncomingRequestForAddresseeOrThrowAsync(
            long id, long addresseeId, bool requirePending)
        {
            var request = await _repo.GetByIdAsync(id)
                ?? throw new EntityNotFoundException("Friend request not found");
            if (request.AddresseeId != addresseeId)
                throw new UnauthorizedAccessException("Only the addressee can act on this friend request.");
            if (requirePending && !request.IsPending)
                throw new ArgumentException("Invalid Friend Request.");
            return request;
        }

        public async Task<List<FriendRequestResponseDto>?> GetPendingAsync()
        {
            var userId = GetUserIdFromLogin();
            var pending = await _repo.GetForUserAsync(userId, isPending: true);
            var ignoredOutgoing = (await _repo.GetForUserAsync(userId, isPending: false))
                .Where(r => r.RequesterId == userId);
            var requests = pending.Concat(ignoredOutgoing).ToList();
            if (requests.Count == 0) return null;
            return await MapRequestsAsync(requests, userId);
        }

        public async Task<List<FriendRequestResponseDto>?> GetIgnoredIncomingAsync()
        {
            var userId = GetUserIdFromLogin();
            var requests = (await _repo.GetForUserAsync(userId, isPending: false))
                .Where(r => r.AddresseeId == userId)
                .ToList();
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
                IsPending: AppearsPendingForViewer(request, viewerId),
                IsIncoming: request.AddresseeId == viewerId,
                CreatedAt: request.CreatedAt,
                UpdatedAt: request.UpdatedAt);

        private static bool AppearsPendingForViewer(FriendRequest request, long viewerId) =>
            request.IsPending || request.RequesterId == viewerId;

        public async Task DeleteRequestByIdAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var request = await _repo.GetByIdAsync(id)
                ?? throw new EntityNotFoundException("Friend request not found");
            if (!request.IsUserInvolved(userId))
                throw new UnauthorizedAccessException("Unauthorized access");
            if (!request.IsPending)
                throw new ArgumentException("Invalid Friend Request.");

            await _repo.DeleteAsync(request);
        }
    }
}
