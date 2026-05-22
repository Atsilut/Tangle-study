using Api.Domain.Friendships.Service;
using Api.Domain.UserBlocks.Domain;
using Api.Domain.UserBlocks.Dto;
using Api.Domain.UserBlocks.Repository;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.UserBlocks.Service
{
    [Service]
    public class UserBlockService
    {
        private readonly IUserBlockRepository _repo;
        private readonly Lazy<FriendRequestService> _friendRequestService;
        private readonly UserService _userService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserBlockService(
            IUserBlockRepository repo,
            Lazy<FriendRequestService> friendRequestService,
            UserService userService,
            IHttpContextAccessor httpContextAccessor)
        {
            _repo = repo;
            _friendRequestService = friendRequestService;
            _userService = userService;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        public async Task BlockUserAsync(UserBlockCreateRequestDto request)
        {
            var blockerId = GetUserIdFromLogin();
            await ValidateBlockPartiesAsync(blockerId, request.BlockedUserId);

            if (!await _repo.ExistsAsync(blockerId, request.BlockedUserId))
            {
                await _repo.CreateAsync(new UserBlock(blockerId, request.BlockedUserId));
            }

            await _friendRequestService.Value.IgnorePendingRequestForUserPairAsync(blockerId, request.BlockedUserId);
        }

        public Task<bool> IsBlockedByAsync(long blockerId, long blockedUserId) =>
            _repo.ExistsAsync(blockerId, blockedUserId);

        private async Task ValidateBlockPartiesAsync(long blockerId, long blockedUserId)
        {
            if (blockerId == blockedUserId)
                throw new ArgumentException("Cannot block yourself.");
            await _userService.EnsureUserExistsAsync(blockerId, "Authentication failed", StatusCodes.Status400BadRequest);
            await _userService.EnsureUserExistsAsync(blockedUserId, "User not found", StatusCodes.Status400BadRequest);
        }
    }
}
