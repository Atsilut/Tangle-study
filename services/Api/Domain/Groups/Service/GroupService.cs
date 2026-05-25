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
    public class GroupService
    {
        private readonly IGroupRepository _repo;
        private readonly IGroupMemberRepository _memberRepo;
        private readonly GroupMembershipService _membership;
        private readonly UserService _userService;
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Lazy<GroupInvitationService> _invitationService;
        private readonly Lazy<GroupApplicationService> _applicationService;

        public GroupService(
            IGroupRepository repo,
            IGroupMemberRepository memberRepo,
            GroupMembershipService membership,
            UserService userService,
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor,
            Lazy<GroupInvitationService> invitationService,
            Lazy<GroupApplicationService> applicationService)
        {
            _repo = repo;
            _memberRepo = memberRepo;
            _membership = membership;
            _userService = userService;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _invitationService = invitationService;
            _applicationService = applicationService;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        private async Task<Group> GetGroupOrThrowAsync(long id)
        {
            var group = await _repo.GetGroupByIdAsync(id);
            if (group is null) throw new EntityNotFoundException("Group not found");
            return group;
        }

        public async Task EnsureGroupExistsAsync(long id, string notFoundMessage = "Group not found", int statusCode = StatusCodes.Status404NotFound)
        {
            if (!await _repo.ExistsGroupByIdAsync(id))
                throw new EntityNotFoundException(notFoundMessage, statusCode);
        }

        public async Task<GroupResponseDto> CreateGroupAsync(GroupCreateRequestDto request)
        {
            var creatorId = GetUserIdFromLogin();
            await _userService.EnsureUserExistsAsync(creatorId, "Authentication failed", StatusCodes.Status400BadRequest);

            var group = new Group(
                request.Name,
                request.Description,
                request.Visibility,
                request.JoinPolicy ?? GroupJoinPolicy.Requestable);
            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _repo.CreateGroupAsync(group);
                await _memberRepo.AddMemberAsync(new GroupMember(group.Id, creatorId, GroupRole.Owner));
            });

            return MapToDto(group, 1);
        }

        public async Task<GroupResponseDto> GetGroupAsync(long id)
        {
            var group = await GetGroupOrThrowAsync(id);
            if (group.Visibility == GroupVisibility.Private)
                await _membership.EnsureMemberAsync(id, GetUserIdFromLogin());

            var memberCount = await _membership.CountMembersAsync(id);
            return MapToDto(group, memberCount);
        }

        public async Task<GroupResponseDto> UpdateGroupAsync(GroupPatchRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureAdminOrOwnerAsync(request.Id, callerId);

            var group = await GetGroupOrThrowAsync(request.Id);
            group.UpdateDetails(request.Name, request.Description, request.Visibility, request.JoinPolicy);
            await _repo.UpdateGroupAsync(group);

            var memberCount = await _membership.CountMembersAsync(group.Id);
            return MapToDto(group, memberCount);
        }

        public async Task<GroupResponseDto> TransferOwnershipAsync(GroupTransferOwnershipRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            var ownerMember = await _membership.EnsureOwnerAsync(request.Id, callerId);
            if (request.NewOwnerUserId == callerId)
                throw new ArgumentException("You already own this group.");

            var newOwner = await _memberRepo.GetMemberAsync(request.Id, request.NewOwnerUserId)
                ?? throw new ArgumentException("Target user is not a member of this group.");

            var group = await GetGroupOrThrowAsync(request.Id);
            await _db.ExecuteInTransactionAsync(async () =>
            {
                ownerMember.ChangeRole(GroupRole.Admin);
                newOwner.ChangeRole(GroupRole.Owner);
                await _memberRepo.UpdateMemberAsync(ownerMember);
                await _memberRepo.UpdateMemberAsync(newOwner);
            });

            var memberCount = await _membership.CountMembersAsync(request.Id);
            return MapToDto(group, memberCount);
        }

        public async Task DeleteGroupAsync(long id)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureOwnerAsync(id, callerId);

            await _db.ExecuteInTransactionAsync(async () =>
            {
                await _invitationService.Value.DeleteAllByGroupAsync(id);
                await _applicationService.Value.DeleteAllByGroupAsync(id);
                await _membership.RemoveAllByGroupAsync(id);
                var group = await GetGroupOrThrowAsync(id);
                await _repo.DeleteGroupAsync(group);
            });
        }

        private static GroupResponseDto MapToDto(Group group, int memberCount) => new(
            Id: group.Id,
            Name: group.Name,
            Description: group.Description,
            Visibility: group.Visibility,
            JoinPolicy: group.JoinPolicy,
            MemberCount: memberCount,
            CreatedAt: group.CreatedAt,
            UpdatedAt: group.UpdatedAt);
    }
}
