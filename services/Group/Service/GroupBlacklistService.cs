using Group.Entities;
using Group.Dto;
using Group.Repository;
using Group.Client;
using Group.Db;
using Tangle.AspNetCore.Auth;
using Tangle.AspNetCore.Exceptions;
using Group.Infrastructure;
using Tangle.AspNetCore.Db;

namespace Group.Service
{
    [Service]
    public class GroupBlacklistService(
        IGroupBlacklistRepository repo,
        Lazy<GroupService> groupService,
        GroupMembershipService membershipService,
        Lazy<GroupJoinResolutionService> joinResolution,
        IUserClient userClient,
        GroupDbContext db,
        CurrentUserAccessor currentUser)
    {
        private readonly IGroupBlacklistRepository _repo = repo;
        private readonly Lazy<GroupService> _groupService = groupService;
        private readonly GroupMembershipService _membershipService = membershipService;
        private readonly Lazy<GroupJoinResolutionService> _joinResolution = joinResolution;
        private readonly IUserClient _userClient = userClient;
        private readonly GroupDbContext _db = db;
        private readonly CurrentUserAccessor _currentUser = currentUser;

        private long GetUserIdFromLogin() => _currentUser.GetUserIdFromLogin();

        public Task<bool> IsBlacklistedAsync(long groupId, long userId) =>
            _repo.ExistsAsync(groupId, userId);

        public async Task EnsureNotBlacklistedAsync(long groupId, long userId, string? message = null)
        {
            if (await _repo.ExistsAsync(groupId, userId)) throw new ArgumentException(message ?? "This user is blacklisted from the group.");
        }

        public async Task<GroupBlacklistGetResponseDto> AddAsync(long groupId, GroupBlacklistCreateRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureOwnerAsync(groupId, callerId);

            await _groupService.Value.EnsureGroupExistsAsync(groupId);

            if (callerId == request.UserId) throw new ArgumentException("Cannot blacklist yourself.");

            await _userClient.EnsureUserExistsAsync(request.UserId, "User not found", StatusCodes.Status400BadRequest);

            if (await _repo.ExistsAsync(groupId, request.UserId)) throw new EntityAlreadyExistsException("User is already blacklisted from this group.");

            if ((await _membershipService.GetMemberAsync(groupId, request.UserId))?.Role == GroupRole.Owner) throw new ArgumentException("Cannot blacklist the group owner. Transfer ownership first.");

            var entry = new GroupBlacklist(groupId, request.UserId);
            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _repo.CreateAsync(entry);
                await _membershipService.RemoveMemberInternalAsync(groupId, request.UserId);
                await _joinResolution.Value.DeleteJoinArtifactsForUserAndGroupAsync(groupId, request.UserId);
            });

            var nicknames = await _userClient.GetNicknamesByUserIdsAsync([request.UserId]);
            var nickname = nicknames.GetValueOrDefault(request.UserId, "Deleted User");
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

        public async Task<List<GroupBlacklistGetResponseDto>> ListAsync(long groupId)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureOwnerAsync(groupId, callerId);

            var entries = await _repo.GetByGroupAsync(groupId);
            if (entries.Count == 0) return [];

            var nicknames = await _userClient.GetNicknamesByUserIdsAsync(entries.Select(e => e.UserId));
            return [.. entries
                .Select(e => MapToDto(e, nicknames.GetValueOrDefault(e.UserId, "Deleted User")))];
        }

        public Task DeleteAllByGroupAsync(long groupId) => _repo.DeleteAllByGroupAsync(groupId);

        private static GroupBlacklistGetResponseDto MapToDto(GroupBlacklist entry, string nickname) => new(
            Id: entry.Id,
            GroupId: entry.GroupId,
            UserId: entry.UserId,
            UserNickname: nickname,
            CreatedAt: entry.CreatedAt,
            UpdatedAt: entry.UpdatedAt);
    }
}
