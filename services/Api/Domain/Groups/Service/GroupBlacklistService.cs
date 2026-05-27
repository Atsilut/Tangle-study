using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Repository;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupBlacklistService
    {
        private readonly IGroupBlacklistRepository _repo;
        private readonly Lazy<GroupService> _groupService;
        private readonly GroupMembershipService _membershipService;
        private readonly Lazy<GroupJoinResolutionService> _joinResolution;
        private readonly UserService _userService;
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GroupBlacklistService(
            IGroupBlacklistRepository repo,
            Lazy<GroupService> groupService,
            GroupMembershipService membershipService,
            Lazy<GroupJoinResolutionService> joinResolution,
            UserService userService,
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor)
        {
            _repo = repo;
            _groupService = groupService;
            _membershipService = membershipService;
            _joinResolution = joinResolution;
            _userService = userService;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

        public Task<bool> IsBlacklistedAsync(long groupId, long userId) =>
            _repo.ExistsAsync(groupId, userId);

        public async Task EnsureNotBlacklistedAsync(long groupId, long userId, string? message = null)
        {
            if (await _repo.ExistsAsync(groupId, userId))
                throw new ArgumentException(message ?? "This user is blacklisted from the group.");
        }

        public async Task<GroupBlacklistResponseDto> AddAsync(long groupId, GroupBlacklistCreateRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureOwnerAsync(groupId, callerId);

            await _groupService.Value.EnsureGroupExistsAsync(groupId);

            if (callerId == request.UserId)
                throw new ArgumentException("Cannot blacklist yourself.");

            await _userService.EnsureUserExistsAsync(request.UserId, "User not found", StatusCodes.Status400BadRequest);

            if (await _repo.ExistsAsync(groupId, request.UserId))
                throw new EntityAlreadyExistsException("User is already blacklisted from this group.");

            if ((await _membershipService.GetMemberAsync(groupId, request.UserId))?.Role == GroupRole.Owner)
                throw new ArgumentException("Cannot blacklist the group owner. Transfer ownership first.");

            var entry = new GroupBlacklist(groupId, request.UserId);
            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _repo.CreateAsync(entry);
                await _membershipService.RemoveMemberInternalAsync(groupId, request.UserId);
                await _joinResolution.Value.DeleteJoinArtifactsForUserAndGroupAsync(groupId, request.UserId);
            });

            var nickname = (await _userService.GetUserByIdAsync(request.UserId))?.Nickname ?? "Deleted User";
            return MapToDto(entry, nickname);
        }

        public async Task RemoveAsync(long groupId, long userId)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureOwnerAsync(groupId, callerId);

            var entry = await _repo.GetAsync(groupId, userId)
                ?? throw new EntityNotFoundException("User is not on the group blacklist.");

            await _repo.DeleteAsync(entry);
        }

        public async Task<List<GroupBlacklistResponseDto>> ListAsync(long groupId)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureOwnerAsync(groupId, callerId);

            var entries = await _repo.GetByGroupAsync(groupId);
            if (entries.Count == 0) return new List<GroupBlacklistResponseDto>();

            var nicknames = await _userService.GetNicknamesByUserIdsAsync(entries.Select(e => e.UserId));
            return entries
                .Select(e => MapToDto(e, nicknames.GetValueOrDefault(e.UserId, "Deleted User")))
                .ToList();
        }

        public Task DeleteAllByGroupAsync(long groupId) => _repo.DeleteAllByGroupAsync(groupId);

        private static GroupBlacklistResponseDto MapToDto(GroupBlacklist entry, string nickname) => new(
            Id: entry.Id,
            GroupId: entry.GroupId,
            UserId: entry.UserId,
            UserNickname: nickname,
            CreatedAt: entry.CreatedAt,
            UpdatedAt: entry.UpdatedAt);
    }
}
