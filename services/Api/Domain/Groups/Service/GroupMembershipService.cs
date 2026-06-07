using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Repository;
using Api.Domain.Users.Service;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupMembershipService(
        IGroupMemberRepository repo,
        Lazy<GroupService> groupService,
        UserService userService,
        IHttpContextAccessor httpContextAccessor)
    {
        private readonly IGroupMemberRepository _repo = repo;
        private readonly Lazy<GroupService> _groupService = groupService;
        private readonly UserService _userService = userService;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

        public Task<GroupMember?> GetMemberAsync(long groupId, long userId) =>
            _repo.GetMemberAsync(groupId, userId);

        public async Task<bool> IsMemberAsync(long groupId, long userId) =>
            await _repo.GetMemberAsync(groupId, userId) is not null;

        public async Task EnsureMemberAsync(long groupId, long userId, string notFoundMessage = "Group not found")
        {
            var member = await _repo.GetMemberAsync(groupId, userId);
            if (member is null) throw new EntityNotFoundException(notFoundMessage);
        }

        public async Task<GroupMember> EnsureAdminOrOwnerAsync(long groupId, long userId)
        {
            var member = await _repo.GetMemberAsync(groupId, userId);
            if (member is null)
                throw new UnauthorizedAccessException("Unauthorized access");
            if (member.Role != GroupRole.Admin && member.Role != GroupRole.Owner)
                throw new UnauthorizedAccessException("Unauthorized access");
            return member;
        }

        public async Task<GroupMember> EnsureOwnerAsync(long groupId, long userId)
        {
            var member = await _repo.GetMemberAsync(groupId, userId);
            if (member is null || member.Role != GroupRole.Owner)
                throw new UnauthorizedAccessException("Unauthorized access");
            return member;
        }

        public Task<int> CountMembersAsync(long groupId) => _repo.CountMembersAsync(groupId);

        public Task AddMemberInternalAsync(long groupId, long userId, GroupRole role) =>
            _repo.AddMemberAsync(new GroupMember(groupId, userId, role));

        public Task RemoveAllByGroupAsync(long groupId) => _repo.RemoveAllByGroupAsync(groupId);

        public async Task TransferOwnershipInternalAsync(
            long groupId,
            GroupMember currentOwner,
            GroupMember newOwner)
        {
            currentOwner.ChangeRole(GroupRole.Admin);
            newOwner.ChangeRole(GroupRole.Owner);
            await _repo.UpdateMemberAsync(currentOwner);
            await _repo.UpdateMemberAsync(newOwner);
        }

        public async Task RemoveMemberInternalAsync(long groupId, long userId)
        {
            var member = await _repo.GetMemberAsync(groupId, userId);
            if (member is not null)
                await _repo.RemoveMemberAsync(member);
        }

        public async Task HandleUserDeletionAsync(long userId)
        {
            var memberships = await _repo.GetMembershipsByUserAsync(userId);

            foreach (var owned in memberships.Where(m => m.Role == GroupRole.Owner))
            {
                var members = await _repo.GetMembersByGroupAsync(owned.GroupId);
                var successor = members
                    .Where(m => m.UserId != userId)
                    .OrderByDescending(m => m.Role)
                    .ThenBy(m => m.JoinedAt)
                    .FirstOrDefault();

                if (successor is null)
                    await _groupService.Value.DeleteGroupInternalAsync(owned.GroupId);
                else
                    await TransferOwnershipInternalAsync(owned.GroupId, owned, successor);
            }

            await _repo.RemoveAllByUserAsync(userId);
        }

        public async Task<List<GroupMemberResponseDto>?> GetMembersAsync(long groupId)
        {
            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            if (group.Visibility == GroupVisibility.Private)
                await EnsureMemberAsync(groupId, GetUserIdFromLogin());

            var members = await _repo.GetMembersByGroupAsync(groupId);
            if (members.Count == 0) return null;
            return await MapManyAsync(members);
        }

        public async Task<GroupMemberResponseDto> UpdateRoleAsync(long groupId, long userId, GroupMemberRolePatchRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await EnsureOwnerAsync(groupId, callerId);

            if (userId == callerId)
                throw new ArgumentException("Owner cannot change their own role. Use transfer ownership instead.");
            if (request.Role == GroupRole.Owner)
                throw new ArgumentException("Owner role cannot be assigned directly. Use transfer ownership.");

            var target = await _repo.GetMemberAsync(groupId, userId)
                ?? throw new ArgumentException("Target user is not a member of this group.");
            if (target.Role == GroupRole.Owner)
                throw new ArgumentException("Cannot demote the owner without transferring ownership.");

            target.ChangeRole(request.Role);
            await _repo.UpdateMemberAsync(target);

            var nickname = (await _userService.GetUserByIdAsync(target.UserId))?.Nickname ?? "Deleted User";
            return MapToDto(target, nickname);
        }

        public async Task RemoveMemberAsync(long groupId, long userId)
        {
            var callerId = GetUserIdFromLogin();
            var callerMember = await _repo.GetMemberAsync(groupId, callerId)
                ?? throw new UnauthorizedAccessException("Unauthorized access");

            var target = await _repo.GetMemberAsync(groupId, userId)
                ?? throw new ArgumentException("Target user is not a member of this group.");

            if (target.Role == GroupRole.Owner)
                throw new ArgumentException("Owner cannot be removed. Transfer ownership first.");

            var isSelf = callerId == userId;
            if (isSelf)
            {
                await _repo.RemoveMemberAsync(target);
                return;
            }

            if (callerMember.Role == GroupRole.Member)
                throw new UnauthorizedAccessException("Unauthorized access");
            if (target.Role == GroupRole.Admin && callerMember.Role != GroupRole.Owner)
                throw new UnauthorizedAccessException("Unauthorized access");

            await _repo.RemoveMemberAsync(target);
        }

        private static GroupMemberResponseDto MapToDto(GroupMember member, string nickname) =>
            new(
                UserId: member.UserId,
                Nickname: nickname,
                Role: member.Role,
                CreatedAt: member.JoinedAt,
                UpdatedAt: member.UpdatedAt);

        private async Task<List<GroupMemberResponseDto>> MapManyAsync(IReadOnlyList<GroupMember> members)
        {
            var nicknames = await _userService.GetNicknamesByUserIdsAsync(members.Select(m => m.UserId));
            return [.. members
                .OrderByDescending(m => m.Role)
                .ThenBy(m => m.JoinedAt)
                .Select(m => MapToDto(m, nicknames.GetValueOrDefault(m.UserId, "Deleted User")))];
        }
    }
}
