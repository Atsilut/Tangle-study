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
        private readonly IGroupBlacklistRepository _blacklistRepo;
        private readonly IGroupRepository _groupRepo;
        private readonly IGroupMemberRepository _memberRepo;
        private readonly GroupMembershipService _membershipService;
        private readonly GroupJoinResolutionService _joinResolution;
        private readonly UserService _userService;
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GroupBlacklistService(
            IGroupBlacklistRepository blacklistRepo,
            IGroupRepository groupRepo,
            IGroupMemberRepository memberRepo,
            GroupMembershipService membershipService,
            GroupJoinResolutionService joinResolution,
            UserService userService,
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor)
        {
            _blacklistRepo = blacklistRepo;
            _groupRepo = groupRepo;
            _memberRepo = memberRepo;
            _membershipService = membershipService;
            _joinResolution = joinResolution;
            _userService = userService;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        public async Task EnsureNotBlacklistedAsync(long groupId, long userId, string? message = null)
        {
            if (await _blacklistRepo.ExistsAsync(groupId, userId))
                throw new ArgumentException(message ?? "This user is blacklisted from the group.");
        }

        public async Task<GroupBlacklistResponseDto> AddAsync(long groupId, GroupBlacklistCreateRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureOwnerAsync(groupId, callerId);

            if (await _groupRepo.GetGroupByIdAsync(groupId) is null)
                throw new EntityNotFoundException("Group not found");

            if (callerId == request.UserId)
                throw new ArgumentException("Cannot blacklist yourself.");

            await _userService.EnsureUserExistsAsync(request.UserId, "User not found", StatusCodes.Status400BadRequest);

            if (await _blacklistRepo.ExistsAsync(groupId, request.UserId))
                throw new EntityAlreadyExistsException("User is already blacklisted from this group.");

            if ((await _memberRepo.GetMemberAsync(groupId, request.UserId))?.Role == GroupRole.Owner)
                throw new ArgumentException("Cannot blacklist the group owner. Transfer ownership first.");

            var entry = new GroupBlacklist(groupId, request.UserId);
            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _blacklistRepo.CreateAsync(entry);
                var member = await _memberRepo.GetMemberAsync(groupId, request.UserId);
                if (member is not null)
                    await _memberRepo.RemoveMemberAsync(member);
                await _joinResolution.DeleteJoinArtifactsForUserAndGroupAsync(groupId, request.UserId);
            });

            var nickname = (await _userService.GetUserByIdAsync(request.UserId))?.Nickname ?? "Deleted User";
            return MapToDto(entry, nickname);
        }

        public async Task RemoveAsync(long groupId, long userId)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureOwnerAsync(groupId, callerId);

            var entry = await _blacklistRepo.GetAsync(groupId, userId)
                ?? throw new EntityNotFoundException("User is not on the group blacklist.");

            await _blacklistRepo.DeleteAsync(entry);
        }

        public async Task<List<GroupBlacklistResponseDto>> ListAsync(long groupId)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureOwnerAsync(groupId, callerId);

            var entries = await _blacklistRepo.GetByGroupAsync(groupId);
            if (entries.Count == 0) return new List<GroupBlacklistResponseDto>();

            var nicknames = await _userService.GetNicknamesByUserIdsAsync(entries.Select(e => e.UserId));
            return entries
                .Select(e => MapToDto(e, nicknames.GetValueOrDefault(e.UserId, "Deleted User")))
                .ToList();
        }

        private static GroupBlacklistResponseDto MapToDto(GroupBlacklist entry, string nickname) => new(
            Id: entry.Id,
            GroupId: entry.GroupId,
            UserId: entry.UserId,
            UserNickname: nickname,
            CreatedAt: entry.CreatedAt,
            UpdatedAt: entry.UpdatedAt);
    }
}
