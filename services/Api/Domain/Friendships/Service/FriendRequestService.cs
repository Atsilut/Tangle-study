using Api.Domain.Friendships.Domain;
using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Repository;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Domain.Friendships.Service
{
    [Service]
    public class FriendRequestService(
        IFriendRequestRepository requestRepo,
        FriendshipService friendshipService,
        UserService userService,
        UserBlockService userBlockService,
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor,
        ILogger<FriendRequestService> logger)
    {
        private readonly IFriendRequestRepository _repo = requestRepo;
        private readonly FriendshipService _friendshipService = friendshipService;
        private readonly UserService _userService = userService;
        private readonly UserBlockService _userBlockService = userBlockService;
        private readonly AppDbContext _db = db;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly ILogger<FriendRequestService> _logger = logger;

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

        public async Task<SendFriendRequestOutcome> SendRequestAsync(FriendRequestCreateRequestDto request)
        {
            var requesterId = GetUserIdFromLogin();
            await ValidateSendRequestPartiesAsync(requesterId, request.AddresseeId);

            var existingRequest = await _repo.GetForUserPairAsync(requesterId, request.AddresseeId);
            if (existingRequest is not null)
                return await HandleExistingFriendRequestAsync(requesterId, request.AddresseeId, existingRequest);

            try
            {
                await CreateOutgoingFriendRequestInTransactionAsync(requesterId, request.AddresseeId);
            }
            catch (DbUpdateException ex) when (IsFriendRequestPairUniqueViolation(ex))
            {
                // Concurrent opposite-direction send; resolve using the row that won the race.
            }

            return await ResolveSendOutcomeForUserPairAsync(requesterId, request.AddresseeId);
        }

        private Task CreateOutgoingFriendRequestInTransactionAsync(long requesterId, long addresseeId)
        {
            return _db.ExecuteInTransactionAsync(async () =>
            {
                if (await _repo.GetForUserPairAsync(requesterId, addresseeId) is not null)
                    return;

                await CreateOutgoingFriendRequestAsync(requesterId, addresseeId);
            });
        }

        private async Task<SendFriendRequestOutcome> ResolveSendOutcomeForUserPairAsync(long requesterId, long addresseeId)
        {
            var existingRequest = await _repo.GetForUserPairAsync(requesterId, addresseeId)
                ?? throw new InvalidOperationException("Friend request for user pair was not created.");
            return await HandleExistingFriendRequestAsync(requesterId, addresseeId, existingRequest);
        }

        private bool IsFriendRequestPairUniqueViolation(DbUpdateException exception) =>
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

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
                if (existingRequest.IsPending)
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

        public async Task HandlePendingFriendRequestOnBlockAsync(long blockerId, long blockedUserId)
        {
            var friendRequest = await _repo.GetForUserPairAsync(blockerId, blockedUserId);
            if (friendRequest is null) return;

            if (friendRequest.RequesterId == blockerId)
            {
                await _repo.DeleteFriendRequestAsync(friendRequest);
                return;
            }

            if (!friendRequest.IsPending) return;
            friendRequest.Ignore();
            await _repo.UpdateFriendRequestAsync(friendRequest);
        }

        private async Task CreateOutgoingFriendRequestAsync(long requesterId, long addresseeId)
        {
            var friendRequest = new FriendRequest(requesterId, addresseeId);
            if (await IsAddresseeBlockingRequesterAsync(addresseeId, requesterId))
                friendRequest.Ignore();
            await _repo.CreateFriendRequestAsync(friendRequest);
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
            var request = await GetIncomingRequestForAddresseeOrThrowAsync(id, userId, requirePending: false);
            if (!request.IsPending) return;
            request.Ignore();
            await _repo.UpdateFriendRequestAsync(request);
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
            await _repo.DeleteFriendRequestAsync(request);
        }

        private async Task<FriendRequest> GetIncomingRequestForAddresseeOrThrowAsync(
            long id, long addresseeId, bool requirePending)
        {
            var request = await _repo.GetFriendRequestByIdAsync(id)
                ?? throw new EntityNotFoundException("Friend request not found");
            if (request.AddresseeId != addresseeId)
                throw new UnauthorizedAccessException("Only the addressee can act on this friend request.");
            if (requirePending && !request.IsPending)
                throw new ArgumentException("Invalid Friend Request.");
            return request;
        }

        public async Task<List<FriendRequestGetResponseDto>?> GetPendingAsync()
        {
            var userId = GetUserIdFromLogin();
            var pending = await _repo.GetForUserAsync(userId, isPending: true);
            var ignoredOutgoing = (await _repo.GetForUserAsync(userId, isPending: false))
                .Where(r => r.RequesterId == userId);
            List<FriendRequest> requests = [.. pending, .. ignoredOutgoing];
            if (requests.Count == 0) return null;
            return await MapRequestsAsync(requests, userId);
        }

        public async Task<List<FriendRequestGetResponseDto>?> GetIgnoredIncomingAsync()
        {
            var userId = GetUserIdFromLogin();
            List<FriendRequest> requests = [.. (await _repo.GetForUserAsync(userId, isPending: false))
                .Where(r => r.AddresseeId == userId)];
            if (requests.Count == 0) return null;
            return await MapRequestsAsync(requests, userId);
        }

        private async Task<List<FriendRequestGetResponseDto>> MapRequestsAsync(IReadOnlyList<FriendRequest> requests, long viewerId)
        {
            var otherIds = requests.Select(r => r.OtherPartyId(viewerId)).Distinct();
            var nicknames = await _userService.GetNicknamesByUserIdsAsync(otherIds);

            return [.. requests.Select(r =>
                MapRequestToDto(r, viewerId, nicknames.GetValueOrDefault(r.OtherPartyId(viewerId), "Deleted User")))];
        }

        private FriendRequestGetResponseDto MapRequestToDto(FriendRequest request, long viewerId, string otherUserNickname) =>
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

        private bool AppearsPendingForViewer(FriendRequest request, long viewerId) =>
            request.IsPending || request.RequesterId == viewerId;

        public async Task DeleteRequestByIdAsync(long id)
        {
            var userId = GetUserIdFromLogin();
            var request = await _repo.GetFriendRequestByIdAsync(id)
                ?? throw new EntityNotFoundException("Friend request not found");
            if (!request.IsUserInvolved(userId))
                throw new UnauthorizedAccessException("Unauthorized access");
            if (!request.IsPending)
                throw new ArgumentException("Invalid Friend Request.");

            await _repo.DeleteFriendRequestAsync(request);
        }
    }
}
