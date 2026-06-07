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
    public class GroupApplicationService(
        IGroupApplicationRepository repo,
        Lazy<GroupInvitationService> groupInvitationService,
        Lazy<GroupService> groupService,
        GroupMembershipService membershipService,
        Lazy<GroupJoinResolutionService> joinResolution,
        GroupBlacklistService blacklistService,
        UserService userService,
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor)
    {
        private readonly IGroupApplicationRepository _repo = repo;
        private readonly Lazy<GroupInvitationService> _groupInvitationService = groupInvitationService;
        private readonly Lazy<GroupService> _groupService = groupService;
        private readonly GroupMembershipService _membershipService = membershipService;
        private readonly Lazy<GroupJoinResolutionService> _joinResolution = joinResolution;
        private readonly GroupBlacklistService _blacklistService = blacklistService;
        private readonly UserService _userService = userService;
        private readonly AppDbContext _db = db;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

        public Task<GroupApplication?> GetPendingForUserAsync(long groupId, long applicantId) =>
            _repo.GetPendingForUserAsync(groupId, applicantId);

        public Task DeleteAllForUserAndGroupAsync(long groupId, long userId) =>
            _repo.DeleteAllForUserAndGroupAsync(groupId, userId);

        public async Task<GroupApplicationResult> ApplyAsync(long groupId)
        {
            var applicantId = GetUserIdFromLogin();
            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            GroupJoinPolicyRules.EnsureCanApply(group.JoinPolicy);

            if (await _membershipService.IsMemberAsync(groupId, applicantId))
                throw new EntityAlreadyExistsException("You are already a member of this group.");

            await _blacklistService.EnsureNotBlacklistedAsync(groupId, applicantId);

            var existingApplication = await _repo.GetForUserAsync(groupId, applicantId);
            if (existingApplication is not null)
                return new GroupApplicationResult(
                    GroupApplicationOutcome.GroupApplicationCreated,
                    await MapToDtoAsync(existingApplication, applicantId));

            var pendingInvitation = await _groupInvitationService.Value.GetPendingForUserAsync(groupId, applicantId);
            if (pendingInvitation is not null)
            {
                await _joinResolution.Value.CreateMembershipFromJoinRequestsAsync(groupId, applicantId);
                return new GroupApplicationResult(GroupApplicationOutcome.GroupMembershipCreatedFromReciprocalInvitation, null);
            }

            try
            {
                await CreateApplicationInTransactionAsync(groupId, applicantId);
            }
            catch (DbUpdateException ex) when (IsGroupApplicationUniqueViolation(ex))
            {
                // Concurrent apply; resolve using the row that won the race.
            }

            return await ResolveApplyOutcomeAsync(groupId, applicantId);
        }

        private Task CreateApplicationInTransactionAsync(long groupId, long applicantId)
        {
            return _db.ExecuteInTransactionAsync(async () =>
            {
                if (await _repo.GetForUserAsync(groupId, applicantId) is not null)
                    return;

                await _repo.CreateApplicationAsync(new GroupApplication(groupId, applicantId));
            });
        }

        private async Task<GroupApplicationResult> ResolveApplyOutcomeAsync(long groupId, long applicantId)
        {
            var pendingInvitation = await _groupInvitationService.Value.GetPendingForUserAsync(groupId, applicantId);
            if (pendingInvitation is not null)
            {
                await _joinResolution.Value.CreateMembershipFromJoinRequestsAsync(groupId, applicantId);
                return new GroupApplicationResult(GroupApplicationOutcome.GroupMembershipCreatedFromReciprocalInvitation, null);
            }

            var application = await _repo.GetForUserAsync(groupId, applicantId)
                ?? throw new InvalidOperationException("Group application was not created.");
            return new GroupApplicationResult(
                GroupApplicationOutcome.GroupApplicationCreated,
                await MapToDtoAsync(application, applicantId));
        }

        private static bool IsGroupApplicationUniqueViolation(DbUpdateException exception) =>
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

        public async Task IgnoreAsync(long applicationId)
        {
            var callerId = GetUserIdFromLogin();
            var application = await _repo.GetByIdAsync(applicationId)
                ?? throw new EntityNotFoundException("Application not found");
            await _membershipService.EnsureAdminOrOwnerAsync(application.GroupId, callerId);
            if (!application.IsPending) return;
            application.Ignore();
            await _repo.UpdateApplicationAsync(application);
        }

        public async Task ApproveAsync(long applicationId)
        {
            var callerId = GetUserIdFromLogin();
            var application = await _repo.GetByIdAsync(applicationId)
                ?? throw new EntityNotFoundException("Application not found");
            await _membershipService.EnsureAdminOrOwnerAsync(application.GroupId, callerId);

            if (await _membershipService.IsMemberAsync(application.GroupId, application.ApplicantId))
                return;

            await _blacklistService.EnsureNotBlacklistedAsync(application.GroupId, application.ApplicantId);
            await _joinResolution.Value.CreateMembershipFromJoinRequestsAsync(application.GroupId, application.ApplicantId);
        }

        public async Task RejectAsync(long applicationId)
        {
            var callerId = GetUserIdFromLogin();
            var application = await _repo.GetByIdAsync(applicationId)
                ?? throw new EntityNotFoundException("Application not found");
            await _membershipService.EnsureAdminOrOwnerAsync(application.GroupId, callerId);

            await _repo.DeleteApplicationAsync(application);
        }

        public async Task CancelAsync(long applicationId)
        {
            var userId = GetUserIdFromLogin();
            var application = await _repo.GetByIdAsync(applicationId)
                ?? throw new EntityNotFoundException("Application not found");
            if (application.ApplicantId != userId)
                throw new UnauthorizedAccessException("Only the applicant can cancel this application.");
            if (!application.IsPending)
                throw new ArgumentException("Invalid application.");

            await _repo.DeleteApplicationAsync(application);
        }

        public async Task<List<GroupApplicationResponseDto>> GetPendingByGroupAsync(long groupId)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureAdminOrOwnerAsync(groupId, callerId);

            var applications = await _repo.GetPendingByGroupAsync(groupId);
            return await MapApplicationsForReviewerAsync(applications);
        }

        public async Task<List<GroupApplicationResponseDto>?> GetIgnoredByGroupAsync(long groupId)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureAdminOrOwnerAsync(groupId, callerId);

            var applications = await _repo.GetIgnoredByGroupAsync(groupId);
            if (applications.Count == 0) return null;
            return await MapApplicationsForReviewerAsync(applications);
        }

        public async Task<List<GroupApplicationResponseDto>?> GetMyApplicationsAsync()
        {
            var applicantId = GetUserIdFromLogin();
            var pending = await _repo.GetPendingForApplicantAsync(applicantId);
            var ignoredOutgoing = await _repo.GetIgnoredOutgoingForApplicantAsync(applicantId);
            List<GroupApplication> applications = [.. pending, .. ignoredOutgoing];
            if (applications.Count == 0) return null;
            return await MapApplicationsForApplicantAsync(applications, applicantId);
        }

        public Task DeleteAllByGroupAsync(long groupId) => _repo.DeleteAllByGroupAsync(groupId);

        public Task DeleteAllByUserAsync(long userId) => _repo.DeleteAllByUserAsync(userId);

        private async Task<List<GroupApplicationResponseDto>> MapApplicationsForReviewerAsync(
            IReadOnlyList<GroupApplication> applications)
        {
            if (applications.Count == 0) return [];

            var nicknames = await _userService.GetNicknamesByUserIdsAsync(applications.Select(a => a.ApplicantId));
            return [.. applications
                .Select(a => MapForReviewer(a, nicknames.GetValueOrDefault(a.ApplicantId, "Deleted User")))];
        }

        private async Task<List<GroupApplicationResponseDto>> MapApplicationsForApplicantAsync(
            IReadOnlyList<GroupApplication> applications, long applicantId)
        {
            var nicknames = await _userService.GetNicknamesByUserIdsAsync([applicantId]);
            var nickname = nicknames.GetValueOrDefault(applicantId, "Deleted User");
            return [.. applications.Select(a => MapForApplicant(a, applicantId, nickname))];
        }

        private async Task<GroupApplicationResponseDto> MapToDtoAsync(GroupApplication application, long viewerId)
        {
            var nickname = (await _userService.GetUserByIdAsync(application.ApplicantId))?.Nickname ?? "Deleted User";
            return MapForApplicant(application, viewerId, nickname);
        }

        private static GroupApplicationResponseDto MapForReviewer(GroupApplication application, string applicantNickname) => new(
            Id: application.Id,
            GroupId: application.GroupId,
            ApplicantId: application.ApplicantId,
            ApplicantNickname: applicantNickname,
            IsPending: application.IsPending,
            IsIncoming: true,
            CreatedAt: application.CreatedAt,
            UpdatedAt: application.UpdatedAt);

        private static GroupApplicationResponseDto MapForApplicant(
            GroupApplication application, long viewerId, string applicantNickname) => new(
            Id: application.Id,
            GroupId: application.GroupId,
            ApplicantId: application.ApplicantId,
            ApplicantNickname: applicantNickname,
            IsPending: AppearsPendingForViewer(application, viewerId),
            IsIncoming: false,
            CreatedAt: application.CreatedAt,
            UpdatedAt: application.UpdatedAt);

        private static bool AppearsPendingForViewer(GroupApplication application, long viewerId) =>
            application.IsPending || application.ApplicantId == viewerId;
    }
}
