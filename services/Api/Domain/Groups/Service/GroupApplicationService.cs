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
    public class GroupApplicationService
    {
        private readonly IGroupApplicationRepository _applicationRepo;
        private readonly IGroupInvitationRepository _invitationRepo;
        private readonly IGroupRepository _groupRepo;
        private readonly GroupMembershipService _membershipService;
        private readonly GroupJoinResolutionService _joinResolution;
        private readonly GroupBlacklistService _blacklistService;
        private readonly UserService _userService;
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GroupApplicationService(
            IGroupApplicationRepository applicationRepo,
            IGroupInvitationRepository invitationRepo,
            IGroupRepository groupRepo,
            GroupMembershipService membershipService,
            GroupJoinResolutionService joinResolution,
            GroupBlacklistService blacklistService,
            UserService userService,
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor)
        {
            _applicationRepo = applicationRepo;
            _invitationRepo = invitationRepo;
            _groupRepo = groupRepo;
            _membershipService = membershipService;
            _joinResolution = joinResolution;
            _blacklistService = blacklistService;
            _userService = userService;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        public Task<GroupApplication?> GetPendingForUserAsync(long groupId, long applicantId) =>
            _applicationRepo.GetPendingForUserAsync(groupId, applicantId);

        public async Task<GroupApplicationResult> ApplyAsync(long groupId)
        {
            var applicantId = GetUserIdFromLogin();
            var group = await _groupRepo.GetGroupByIdAsync(groupId)
                ?? throw new EntityNotFoundException("Group not found");

            GroupJoinPolicyRules.EnsureCanApply(group.JoinPolicy);

            if (await _membershipService.IsMemberAsync(groupId, applicantId))
                throw new EntityAlreadyExistsException("You are already a member of this group.");

            await _blacklistService.EnsureNotBlacklistedAsync(groupId, applicantId);

            var existingApplication = await _applicationRepo.GetForUserAsync(groupId, applicantId);
            if (existingApplication is not null)
                return new GroupApplicationResult(
                    GroupApplicationOutcome.GroupApplicationCreated,
                    await MapToDtoAsync(existingApplication, applicantId));

            var pendingInvitation = await _invitationRepo.GetPendingForUserAsync(groupId, applicantId);
            if (pendingInvitation is not null)
            {
                await _joinResolution.CreateMembershipFromJoinRequestsAsync(groupId, applicantId);
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

        private async Task CreateApplicationInTransactionAsync(long groupId, long applicantId)
        {
            await _db.ExecuteInTransactionAsync(async () =>
            {
                if (await _applicationRepo.GetForUserAsync(groupId, applicantId) is not null)
                    return;

                await _applicationRepo.CreateApplicationAsync(new GroupApplication(groupId, applicantId));
            });
        }

        private async Task<GroupApplicationResult> ResolveApplyOutcomeAsync(long groupId, long applicantId)
        {
            var pendingInvitation = await _invitationRepo.GetPendingForUserAsync(groupId, applicantId);
            if (pendingInvitation is not null)
            {
                await _joinResolution.CreateMembershipFromJoinRequestsAsync(groupId, applicantId);
                return new GroupApplicationResult(GroupApplicationOutcome.GroupMembershipCreatedFromReciprocalInvitation, null);
            }

            var application = await _applicationRepo.GetForUserAsync(groupId, applicantId)
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
            var application = await _applicationRepo.GetByIdAsync(applicationId)
                ?? throw new EntityNotFoundException("Application not found");
            await _membershipService.EnsureAdminOrOwnerAsync(application.GroupId, callerId);
            if (!application.IsPending) return;
            application.Ignore();
            await _applicationRepo.UpdateApplicationAsync(application);
        }

        public async Task ApproveAsync(long applicationId)
        {
            var callerId = GetUserIdFromLogin();
            var application = await _applicationRepo.GetByIdAsync(applicationId)
                ?? throw new EntityNotFoundException("Application not found");
            await _membershipService.EnsureAdminOrOwnerAsync(application.GroupId, callerId);

            if (await _membershipService.IsMemberAsync(application.GroupId, application.ApplicantId))
                return;

            await _blacklistService.EnsureNotBlacklistedAsync(application.GroupId, application.ApplicantId);
            await _joinResolution.CreateMembershipFromJoinRequestsAsync(application.GroupId, application.ApplicantId);
        }

        public async Task RejectAsync(long applicationId)
        {
            var callerId = GetUserIdFromLogin();
            var application = await _applicationRepo.GetByIdAsync(applicationId)
                ?? throw new EntityNotFoundException("Application not found");
            await _membershipService.EnsureAdminOrOwnerAsync(application.GroupId, callerId);

            await _applicationRepo.DeleteApplicationAsync(application);
        }

        public async Task CancelAsync(long applicationId)
        {
            var userId = GetUserIdFromLogin();
            var application = await _applicationRepo.GetByIdAsync(applicationId)
                ?? throw new EntityNotFoundException("Application not found");
            if (application.ApplicantId != userId)
                throw new UnauthorizedAccessException("Only the applicant can cancel this application.");
            if (!application.IsPending)
                throw new ArgumentException("Invalid application.");

            await _applicationRepo.DeleteApplicationAsync(application);
        }

        public async Task<List<GroupApplicationResponseDto>> GetPendingByGroupAsync(long groupId)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureAdminOrOwnerAsync(groupId, callerId);

            var applications = await _applicationRepo.GetPendingByGroupAsync(groupId);
            return await MapApplicationsForReviewerAsync(applications);
        }

        public async Task<List<GroupApplicationResponseDto>?> GetIgnoredByGroupAsync(long groupId)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureAdminOrOwnerAsync(groupId, callerId);

            var applications = await _applicationRepo.GetIgnoredByGroupAsync(groupId);
            if (applications.Count == 0) return null;
            return await MapApplicationsForReviewerAsync(applications);
        }

        public async Task<List<GroupApplicationResponseDto>?> GetMyApplicationsAsync()
        {
            var applicantId = GetUserIdFromLogin();
            var pending = await _applicationRepo.GetPendingForApplicantAsync(applicantId);
            var ignoredOutgoing = await _applicationRepo.GetIgnoredOutgoingForApplicantAsync(applicantId);
            var applications = pending.Concat(ignoredOutgoing).ToList();
            if (applications.Count == 0) return null;
            return await MapApplicationsForApplicantAsync(applications, applicantId);
        }

        public Task DeleteAllByGroupAsync(long groupId) => _applicationRepo.DeleteAllByGroupAsync(groupId);

        public Task DeleteAllByUserAsync(long userId) => _applicationRepo.DeleteAllByUserAsync(userId);

        private async Task<List<GroupApplicationResponseDto>> MapApplicationsForReviewerAsync(
            IReadOnlyList<GroupApplication> applications)
        {
            if (applications.Count == 0) return new List<GroupApplicationResponseDto>();

            var nicknames = await _userService.GetNicknamesByUserIdsAsync(applications.Select(a => a.ApplicantId));
            return applications
                .Select(a => MapForReviewer(a, nicknames.GetValueOrDefault(a.ApplicantId, "Deleted User")))
                .ToList();
        }

        private async Task<List<GroupApplicationResponseDto>> MapApplicationsForApplicantAsync(
            IReadOnlyList<GroupApplication> applications, long applicantId)
        {
            var nicknames = await _userService.GetNicknamesByUserIdsAsync(new[] { applicantId });
            var nickname = nicknames.GetValueOrDefault(applicantId, "Deleted User");
            return applications.Select(a => MapForApplicant(a, applicantId, nickname)).ToList();
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
