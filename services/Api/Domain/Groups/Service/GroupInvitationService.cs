using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Repository;
using Api.Domain.Users.Service;
using Api.Global.Db;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupInvitationService
    {
        private readonly IGroupInvitationRepository _invitationRepo;
        private readonly IGroupApplicationRepository _applicationRepo;
        private readonly IGroupRepository _groupRepo;
        private readonly GroupMembershipService _membershipService;
        private readonly GroupJoinResolutionService _joinResolution;
        private readonly UserService _userService;
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GroupInvitationService(
            IGroupInvitationRepository invitationRepo,
            IGroupApplicationRepository applicationRepo,
            IGroupRepository groupRepo,
            GroupMembershipService membershipService,
            GroupJoinResolutionService joinResolution,
            UserService userService,
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor)
        {
            _invitationRepo = invitationRepo;
            _applicationRepo = applicationRepo;
            _groupRepo = groupRepo;
            _membershipService = membershipService;
            _joinResolution = joinResolution;
            _userService = userService;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        private async Task<GroupInvitation> GetInvitationOrThrowAsync(long id)
        {
            var invitation = await _invitationRepo.GetByIdAsync(id);
            if (invitation is null) throw new EntityNotFoundException("Invitation not found");
            return invitation;
        }

        public Task<GroupInvitation?> GetPendingForUserAsync(long groupId, long inviteeId) =>
            _invitationRepo.GetPendingForUserAsync(groupId, inviteeId);

        public async Task<GroupInvitationResult> InviteAsync(long groupId, GroupInvitationCreateRequestDto request)
        {
            var inviterId = GetUserIdFromLogin();
            await _membershipService.EnsureAdminOrOwnerAsync(groupId, inviterId);

            if (inviterId == request.InviteeId)
                throw new ArgumentException("Cannot invite yourself.");

            await _userService.EnsureUserExistsAsync(request.InviteeId, "Invitee not found", StatusCodes.Status400BadRequest);

            if (await _membershipService.IsMemberAsync(groupId, request.InviteeId))
                throw new EntityAlreadyExistsException("User is already a member of this group.");

            var existingInvitation = await _invitationRepo.GetPendingForUserAsync(groupId, request.InviteeId);
            if (existingInvitation is not null)
                return new GroupInvitationResult(
                    GroupInvitationOutcome.GroupInvitationCreated,
                    await MapToDtoAsync(existingInvitation));

            var pendingApplication = await _applicationRepo.GetPendingForUserAsync(groupId, request.InviteeId);
            if (pendingApplication is not null)
            {
                await _joinResolution.CreateMembershipFromJoinRequestsAsync(groupId, request.InviteeId);
                return new GroupInvitationResult(GroupInvitationOutcome.GroupMembershipCreatedFromReciprocalApplication, null);
            }

            try
            {
                await CreateInvitationInTransactionAsync(groupId, inviterId, request.InviteeId);
            }
            catch (DbUpdateException ex) when (IsGroupInvitationUniqueViolation(ex))
            {
                // Concurrent invite; resolve using the row that won the race.
            }

            return await ResolveInviteOutcomeAsync(groupId, request.InviteeId);
        }

        private async Task CreateInvitationInTransactionAsync(long groupId, long inviterId, long inviteeId)
        {
            await _db.ExecuteInTransactionAsync(async () =>
            {
                if (await _invitationRepo.GetPendingForUserAsync(groupId, inviteeId) is not null)
                    return;

                await _invitationRepo.CreateInvitationAsync(new GroupInvitation(groupId, inviterId, inviteeId));
            });
        }

        private async Task<GroupInvitationResult> ResolveInviteOutcomeAsync(long groupId, long inviteeId)
        {
            var pendingApplication = await _applicationRepo.GetPendingForUserAsync(groupId, inviteeId);
            if (pendingApplication is not null)
            {
                await _joinResolution.CreateMembershipFromJoinRequestsAsync(groupId, inviteeId);
                return new GroupInvitationResult(GroupInvitationOutcome.GroupMembershipCreatedFromReciprocalApplication, null);
            }

            var invitation = await _invitationRepo.GetPendingForUserAsync(groupId, inviteeId)
                ?? throw new InvalidOperationException("Group invitation was not created.");
            return new GroupInvitationResult(
                GroupInvitationOutcome.GroupInvitationCreated,
                await MapToDtoAsync(invitation));
        }

        private static bool IsGroupInvitationUniqueViolation(DbUpdateException exception) =>
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

        public async Task AcceptAsync(long invitationId)
        {
            var userId = GetUserIdFromLogin();
            var invitation = await GetInvitationOrThrowAsync(invitationId);
            if (await _membershipService.IsMemberAsync(invitation.GroupId, userId))
                return;
            if (invitation.InviteeId != userId)
                throw new UnauthorizedAccessException("Only the invitee can accept this invitation.");

            await _joinResolution.CreateMembershipFromJoinRequestsAsync(invitation.GroupId, userId);
        }

        public async Task RejectAsync(long invitationId)
        {
            var userId = GetUserIdFromLogin();
            var invitation = await GetInvitationOrThrowAsync(invitationId);
            if (invitation.InviteeId != userId)
                throw new UnauthorizedAccessException("Only the invitee can reject this invitation.");

            await _invitationRepo.DeleteInvitationAsync(invitation);
        }

        public async Task CancelAsync(long invitationId)
        {
            var userId = GetUserIdFromLogin();
            var invitation = await GetInvitationOrThrowAsync(invitationId);

            if (invitation.InviterId != userId)
            {
                try
                {
                    await _membershipService.EnsureAdminOrOwnerAsync(invitation.GroupId, userId);
                }
                catch (UnauthorizedAccessException)
                {
                    throw new UnauthorizedAccessException("Only the inviter or a group admin/owner can cancel this invitation.");
                }
            }

            await _invitationRepo.DeleteInvitationAsync(invitation);
        }

        public async Task<List<GroupInvitationCreateResponseDto>> GetMyPendingAsync()
        {
            var userId = GetUserIdFromLogin();
            var invitations = await _invitationRepo.GetPendingIncomingForInviteeAsync(userId);
            if (invitations.Count == 0) return new List<GroupInvitationCreateResponseDto>();

            var groupNames = await GetGroupNamesAsync(invitations.Select(i => i.GroupId));
            return invitations
                .Select(i => Map(i, groupNames.GetValueOrDefault(i.GroupId, "Unknown group")))
                .ToList();
        }

        public Task DeleteAllByGroupAsync(long groupId) => _invitationRepo.DeleteAllByGroupAsync(groupId);

        public Task DeleteAllByUserAsync(long userId) => _invitationRepo.DeleteAllByUserAsync(userId);

        private async Task<GroupInvitationCreateResponseDto> MapToDtoAsync(GroupInvitation invitation)
        {
            var group = await _groupRepo.GetGroupByIdAsync(invitation.GroupId);
            return Map(invitation, group?.Name ?? "Unknown group");
        }

        private static GroupInvitationCreateResponseDto Map(GroupInvitation invitation, string groupName) => new(
            Id: invitation.Id,
            GroupId: invitation.GroupId,
            GroupName: groupName,
            InviterId: invitation.InviterId,
            InviteeId: invitation.InviteeId,
            IsPending: invitation.IsPending,
            CreatedAt: invitation.CreatedAt,
            UpdatedAt: invitation.UpdatedAt);

        private async Task<Dictionary<long, string>> GetGroupNamesAsync(IEnumerable<long> groupIds)
        {
            var ids = groupIds.Distinct().ToList();
            var names = new Dictionary<long, string>();
            foreach (var id in ids)
            {
                var group = await _groupRepo.GetGroupByIdAsync(id);
                if (group is not null) names[id] = group.Name;
            }
            return names;
        }
    }
}
