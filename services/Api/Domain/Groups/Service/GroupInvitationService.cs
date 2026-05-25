using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Repository;
using Api.Domain.UserBlocks.Service;
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
        private readonly GroupBlacklistService _blacklistService;
        private readonly UserBlockService _userBlockService;
        private readonly UserService _userService;
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GroupInvitationService(
            IGroupInvitationRepository invitationRepo,
            IGroupApplicationRepository applicationRepo,
            IGroupRepository groupRepo,
            GroupMembershipService membershipService,
            GroupJoinResolutionService joinResolution,
            GroupBlacklistService blacklistService,
            UserBlockService userBlockService,
            UserService userService,
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor)
        {
            _invitationRepo = invitationRepo;
            _applicationRepo = applicationRepo;
            _groupRepo = groupRepo;
            _membershipService = membershipService;
            _joinResolution = joinResolution;
            _blacklistService = blacklistService;
            _userBlockService = userBlockService;
            _userService = userService;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        private async Task<bool> BlockExistsBetweenUsersAsync(long userId, long otherUserId) =>
            await _userBlockService.IsBlockedByAsync(userId, otherUserId)
            || await _userBlockService.IsBlockedByAsync(otherUserId, userId);

        private async Task EnsureNoBlockExistsBetweenUsersAsync(long userId, long otherUserId)
        {
            if (await BlockExistsBetweenUsersAsync(userId, otherUserId))
                throw new ArgumentException("Cannot join the group while a block exists between you and this user.");
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        private async Task<GroupInvitation> GetIncomingInvitationForInviteeOrThrowAsync(
            long id, long inviteeId, bool requirePending)
        {
            var invitation = await _invitationRepo.GetByIdAsync(id)
                ?? throw new EntityNotFoundException("Invitation not found");
            if (invitation.InviteeId != inviteeId)
                throw new UnauthorizedAccessException("Only the invitee can act on this invitation.");
            if (requirePending && !invitation.IsPending)
                throw new ArgumentException("Invalid invitation.");
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

            await _blacklistService.EnsureNotBlacklistedAsync(groupId, request.InviteeId);

            if (await _userBlockService.IsBlockedByAsync(inviterId, request.InviteeId))
                throw new ArgumentException("Cannot invite a user you have blocked.");

            var existingInvitation = await _invitationRepo.GetForUserAsync(groupId, request.InviteeId);
            if (existingInvitation is not null && existingInvitation.InviterId == inviterId)
                return new GroupInvitationResult(
                    GroupInvitationOutcome.GroupInvitationCreated,
                    await MapToDtoAsync(existingInvitation, inviterId));

            var pendingApplication = await _applicationRepo.GetPendingForUserAsync(groupId, request.InviteeId);
            if (pendingApplication is not null)
                return await ResolveReciprocalApplicationOnInviteAsync(groupId, inviterId, request.InviteeId);

            try
            {
                await CreateInvitationInTransactionAsync(groupId, inviterId, request.InviteeId);
            }
            catch (DbUpdateException ex) when (IsGroupInvitationUniqueViolation(ex))
            {
                // Concurrent invite; resolve using the row that won the race.
            }

            return await ResolveInviteOutcomeAsync(groupId, request.InviteeId, inviterId);
        }

        private async Task CreateInvitationInTransactionAsync(long groupId, long inviterId, long inviteeId)
        {
            await _db.ExecuteInTransactionAsync(async () =>
            {
                if (await _invitationRepo.GetForUserAsync(groupId, inviteeId) is not null)
                    return;

                var invitation = new GroupInvitation(groupId, inviterId, inviteeId);
                if (await _userBlockService.IsBlockedByAsync(inviteeId, inviterId))
                    invitation.Ignore();
                await _invitationRepo.CreateInvitationAsync(invitation);
            });
        }

        private async Task<GroupInvitationResult> ResolveReciprocalApplicationOnInviteAsync(
            long groupId, long inviterId, long inviteeId)
        {
            if (await BlockExistsBetweenUsersAsync(inviterId, inviteeId))
                return await ReturnInvitationCreatedWithoutJoinAsync(groupId, inviterId, inviteeId);

            await EnsureNoBlockExistsBetweenUsersAsync(inviterId, inviteeId);
            await _joinResolution.CreateMembershipFromJoinRequestsAsync(groupId, inviteeId);
            return new GroupInvitationResult(GroupInvitationOutcome.GroupMembershipCreatedFromReciprocalApplication, null);
        }

        private async Task<GroupInvitationResult> ReturnInvitationCreatedWithoutJoinAsync(
            long groupId, long inviterId, long inviteeId)
        {
            if (await _invitationRepo.GetForUserAsync(groupId, inviteeId) is null)
                await CreateInvitationInTransactionAsync(groupId, inviterId, inviteeId);

            var invitation = await _invitationRepo.GetForUserAsync(groupId, inviteeId)
                ?? throw new InvalidOperationException("Group invitation was not created.");
            return new GroupInvitationResult(
                GroupInvitationOutcome.GroupInvitationCreated,
                await MapToDtoAsync(invitation, inviterId));
        }

        private async Task<GroupInvitationResult> ResolveInviteOutcomeAsync(long groupId, long inviteeId, long inviterId)
        {
            var pendingApplication = await _applicationRepo.GetPendingForUserAsync(groupId, inviteeId);
            if (pendingApplication is not null)
                return await ResolveReciprocalApplicationOnInviteAsync(groupId, inviterId, inviteeId);

            var invitation = await _invitationRepo.GetForUserAsync(groupId, inviteeId)
                ?? throw new InvalidOperationException("Group invitation was not created.");
            return new GroupInvitationResult(
                GroupInvitationOutcome.GroupInvitationCreated,
                await MapToDtoAsync(invitation, inviterId));
        }

        private static bool IsGroupInvitationUniqueViolation(DbUpdateException exception) =>
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

        public async Task IgnoreAsync(long invitationId)
        {
            var userId = GetUserIdFromLogin();
            var invitation = await GetIncomingInvitationForInviteeOrThrowAsync(invitationId, userId, requirePending: false);
            if (!invitation.IsPending) return;
            invitation.Ignore();
            await _invitationRepo.UpdateInvitationAsync(invitation);
        }

        public async Task AcceptAsync(long invitationId)
        {
            var userId = GetUserIdFromLogin();
            var invitation = await GetIncomingInvitationForInviteeOrThrowAsync(invitationId, userId, requirePending: false);
            if (await _membershipService.IsMemberAsync(invitation.GroupId, userId))
                return;

            await EnsureNoBlockExistsBetweenUsersAsync(invitation.InviterId, userId);
            await _blacklistService.EnsureNotBlacklistedAsync(invitation.GroupId, userId);
            await _joinResolution.CreateMembershipFromJoinRequestsAsync(invitation.GroupId, userId);
        }

        public async Task RejectAsync(long invitationId)
        {
            var userId = GetUserIdFromLogin();
            var invitation = await GetIncomingInvitationForInviteeOrThrowAsync(invitationId, userId, requirePending: false);
            await _invitationRepo.DeleteInvitationAsync(invitation);
        }

        public async Task CancelAsync(long invitationId)
        {
            var userId = GetUserIdFromLogin();
            var invitation = await _invitationRepo.GetByIdAsync(invitationId)
                ?? throw new EntityNotFoundException("Invitation not found");

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

            if (!invitation.IsPending)
                throw new ArgumentException("Invalid invitation.");

            await _invitationRepo.DeleteInvitationAsync(invitation);
        }

        public async Task<List<GroupInvitationCreateResponseDto>?> GetMyPendingAsync()
        {
            var userId = GetUserIdFromLogin();
            var pending = await _invitationRepo.GetPendingIncomingForInviteeAsync(userId);
            var ignoredOutgoing = await _invitationRepo.GetIgnoredOutgoingForInviterAsync(userId);
            var invitations = pending.Concat(ignoredOutgoing).ToList();
            if (invitations.Count == 0) return null;
            return await MapInvitationsAsync(invitations, userId);
        }

        public async Task<List<GroupInvitationCreateResponseDto>?> GetIgnoredIncomingAsync()
        {
            var userId = GetUserIdFromLogin();
            var invitations = await _invitationRepo.GetIgnoredIncomingForInviteeAsync(userId);
            if (invitations.Count == 0) return null;
            return await MapInvitationsAsync(invitations, userId);
        }

        public Task DeleteAllByGroupAsync(long groupId) => _invitationRepo.DeleteAllByGroupAsync(groupId);

        public Task DeleteAllByUserAsync(long userId) => _invitationRepo.DeleteAllByUserAsync(userId);

        private async Task<GroupInvitationCreateResponseDto> MapToDtoAsync(GroupInvitation invitation, long viewerId)
        {
            var group = await _groupRepo.GetGroupByIdAsync(invitation.GroupId);
            var otherUserId = invitation.InviteeId == viewerId ? invitation.InviterId : invitation.InviteeId;
            var nickname = (await _userService.GetUserByIdAsync(otherUserId))?.Nickname ?? "Deleted User";
            return Map(invitation, group?.Name ?? "Unknown group", viewerId, nickname);
        }

        private async Task<List<GroupInvitationCreateResponseDto>> MapInvitationsAsync(
            IReadOnlyList<GroupInvitation> invitations, long viewerId)
        {
            var groupNames = await GetGroupNamesAsync(invitations.Select(i => i.GroupId));
            var otherUserIds = invitations
                .Select(i => i.InviteeId == viewerId ? i.InviterId : i.InviteeId)
                .Distinct();
            var nicknames = await _userService.GetNicknamesByUserIdsAsync(otherUserIds);

            return invitations
                .Select(i =>
                {
                    var otherUserId = i.InviteeId == viewerId ? i.InviterId : i.InviteeId;
                    return Map(
                        i,
                        groupNames.GetValueOrDefault(i.GroupId, "Unknown group"),
                        viewerId,
                        nicknames.GetValueOrDefault(otherUserId, "Deleted User"));
                })
                .ToList();
        }

        private static GroupInvitationCreateResponseDto Map(
            GroupInvitation invitation,
            string groupName,
            long viewerId,
            string otherUserNickname) => new(
            Id: invitation.Id,
            GroupId: invitation.GroupId,
            GroupName: groupName,
            InviterId: invitation.InviterId,
            InviteeId: invitation.InviteeId,
            OtherUserId: invitation.InviteeId == viewerId ? invitation.InviterId : invitation.InviteeId,
            OtherUserNickname: otherUserNickname,
            IsPending: AppearsPendingForViewer(invitation, viewerId),
            IsIncoming: invitation.InviteeId == viewerId,
            CreatedAt: invitation.CreatedAt,
            UpdatedAt: invitation.UpdatedAt);

        private static bool AppearsPendingForViewer(GroupInvitation invitation, long viewerId) =>
            invitation.IsPending || invitation.InviterId == viewerId;

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
