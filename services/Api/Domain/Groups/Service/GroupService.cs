using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Repository;
using Api.Domain.Posts.Service;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupService(
        IGroupRepository repo,
        GroupMembershipService membership,
        UserService userService,
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor,
        Lazy<GroupInvitationService> invitationService,
        Lazy<GroupApplicationService> applicationService,
        Lazy<GroupBlacklistService> blacklistService,
        Lazy<GroupBoardService> boardService,
        Lazy<PostService> postService)
    {
        private readonly IGroupRepository _repo = repo;
        private readonly GroupMembershipService _membership = membership;
        private readonly UserService _userService = userService;
        private readonly AppDbContext _db = db;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
        private readonly Lazy<GroupInvitationService> _invitationService = invitationService;
        private readonly Lazy<GroupApplicationService> _applicationService = applicationService;
        private readonly Lazy<GroupBlacklistService> _blacklistService = blacklistService;
        private readonly Lazy<GroupBoardService> _boardService = boardService;
        private readonly Lazy<PostService> _postService = postService;

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

        public async Task<Group> GetGroupOrThrowAsync(long id, string notFoundMessage = "Group not found") =>
            await _repo.GetGroupByIdAsync(id) ?? throw new EntityNotFoundException(notFoundMessage);

        public Task<IReadOnlyDictionary<long, string>> GetGroupNamesByIdsAsync(IEnumerable<long> ids) =>
            _repo.GetGroupNamesByIdsAsync(ids);

        public async Task EnsureGroupExistsAsync(long id, string notFoundMessage = "Group not found", int statusCode = StatusCodes.Status404NotFound)
        {
            if (!await _repo.ExistsGroupByIdAsync(id)) throw new EntityNotFoundException(notFoundMessage, statusCode);
        }

        public async Task<GroupResponseDto> CreateGroupAsync(GroupCreateRequestDto request)
        {
            var creatorId = GetUserIdFromLogin();
            await _userService.EnsureUserExistsAsync(creatorId, "Authentication failed", StatusCodes.Status400BadRequest);

            var group = new Group(
                request.Name,
                request.Description,
                request.Visibility,
                request.JoinPolicy ?? GroupJoinPolicy.Requestable,
                request.InvitePolicy ?? GroupInvitePolicy.AdminsOnly);
            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _repo.CreateGroupAsync(group);
                await _membership.AddMemberInternalAsync(group.Id, creatorId, GroupRole.Owner);
            });

            return MapToDto(group, 1);
        }

        public async Task<GroupResponseDto> GetGroupAsync(long id)
        {
            var group = await GetGroupOrThrowAsync(id);
            var memberCount = await _membership.CountMembersAsync(id);
            var userId = GetUserIdFromLogin();

            if (group.Visibility == GroupVisibility.Private
                && !await _membership.IsMemberAsync(id, userId))
                return MapToLimitedDto(group, memberCount);

            return MapToDto(group, memberCount);
        }

        public async Task<List<GroupResponseDto>?> ListDiscoverableGroupsAsync()
        {
            var groups = await _repo.GetPublicGroupsAsync();
            if (groups.Count == 0) return null;
            return await MapManyToDtosAsync(groups);
        }

        public async Task<List<GroupResponseDto>?> ListMyGroupsAsync()
        {
            var userId = GetUserIdFromLogin();
            var memberships = await _membership.GetMembershipsByUserAsync(userId);
            if (memberships.Count == 0) return null;

            var groups = await _repo.GetGroupsByIdsAsync([.. memberships.Select(m => m.GroupId)]);
            if (groups.Count == 0) return null;
            return await MapManyToDtosAsync(groups);
        }

        public async Task<GroupResponseDto> UpdateGroupAsync(GroupPatchRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureAdminOrOwnerAsync(request.Id, callerId);

            var group = await GetGroupOrThrowAsync(request.Id);
            group.UpdateDetails(
                request.Name,
                request.Description,
                request.Visibility,
                request.JoinPolicy,
                request.InvitePolicy);
            await _repo.UpdateGroupAsync(group);

            var memberCount = await _membership.CountMembersAsync(group.Id);
            return MapToDto(group, memberCount);
        }

        public async Task<GroupResponseDto> TransferOwnershipAsync(GroupTransferOwnershipRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            var ownerMember = await _membership.EnsureOwnerAsync(request.Id, callerId);
            if (request.NewOwnerUserId == callerId) throw new ArgumentException("You already own this group.");

            var newOwner = await _membership.GetMemberAsync(request.Id, request.NewOwnerUserId)
                ?? throw new ArgumentException("Target user is not a member of this group.");

            var group = await GetGroupOrThrowAsync(request.Id);
            await _db.ExecuteInTransactionAsync(async () => await _membership.TransferOwnershipInternalAsync(request.Id, ownerMember, newOwner));

            var memberCount = await _membership.CountMembersAsync(request.Id);
            return MapToDto(group, memberCount);
        }

        public async Task DeleteGroupAsync(long id)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureOwnerAsync(id, callerId);

            await _db.ExecuteInTransactionAsync(() => DeleteGroupInternalAsync(id));
        }

        public async Task DeleteGroupInternalAsync(long groupId)
        {
            await _invitationService.Value.DeleteAllByGroupAsync(groupId);
            await _applicationService.Value.DeleteAllByGroupAsync(groupId);
            await _blacklistService.Value.DeleteAllByGroupAsync(groupId);
            await _postService.Value.DeleteAllByGroupAsync(groupId);
            await _boardService.Value.DeleteAllByGroupAsync(groupId);
            await _membership.RemoveAllByGroupAsync(groupId);
            var group = await GetGroupOrThrowAsync(groupId);
            await _repo.DeleteGroupAsync(group);
        }

        private async Task<List<GroupResponseDto>> MapManyToDtosAsync(IReadOnlyList<Group> groups)
        {
            var groupIds = groups.Select(g => g.Id).ToList();
            var memberCounts = await _membership.GetMemberCountsByGroupIdsAsync(groupIds);
            return [.. groups.Select(g => MapToDto(g, memberCounts.GetValueOrDefault(g.Id, 0)))];
        }

        private static GroupResponseDto MapToDto(Group group, int memberCount) => new(
            Id: group.Id,
            Name: group.Name,
            Description: group.Description,
            Visibility: group.Visibility,
            JoinPolicy: group.JoinPolicy,
            InvitePolicy: group.InvitePolicy,
            MemberCount: memberCount,
            CreatedAt: group.CreatedAt,
            UpdatedAt: group.UpdatedAt,
            IsLimitedProfile: false);

        private static GroupResponseDto MapToLimitedDto(Group group, int memberCount) => new(
            Id: group.Id,
            Name: group.Name,
            Description: string.Empty,
            Visibility: group.Visibility,
            JoinPolicy: group.JoinPolicy,
            InvitePolicy: group.InvitePolicy,
            MemberCount: memberCount,
            CreatedAt: default,
            UpdatedAt: default,
            IsLimitedProfile: true);
    }
}
