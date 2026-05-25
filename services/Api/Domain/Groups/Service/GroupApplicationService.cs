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
        private readonly UserService _userService;
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GroupApplicationService(
            IGroupApplicationRepository applicationRepo,
            IGroupInvitationRepository invitationRepo,
            IGroupRepository groupRepo,
            GroupMembershipService membershipService,
            GroupJoinResolutionService joinResolution,
            UserService userService,
            AppDbContext db,
            IHttpContextAccessor httpContextAccessor)
        {
            _applicationRepo = applicationRepo;
            _invitationRepo = invitationRepo;
            _groupRepo = groupRepo;
            _membershipService = membershipService;
            _joinResolution = joinResolution;
            _userService = userService;
            _db = db;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        private async Task<GroupApplication> GetApplicationOrThrowAsync(long id)
        {
            var application = await _applicationRepo.GetByIdAsync(id);
            if (application is null) throw new EntityNotFoundException("Application not found");
            return application;
        }

        public Task<GroupApplication?> GetPendingForUserAsync(long groupId, long applicantId) =>
            _applicationRepo.GetPendingForUserAsync(groupId, applicantId);

        public async Task<GroupApplicationResult> ApplyAsync(long groupId)
        {
            var applicantId = GetUserIdFromLogin();
            if (await _groupRepo.GetGroupByIdAsync(groupId) is null)
                throw new EntityNotFoundException("Group not found");

            if (await _membershipService.IsMemberAsync(groupId, applicantId))
                throw new EntityAlreadyExistsException("You are already a member of this group.");

            var existingApplication = await _applicationRepo.GetPendingForUserAsync(groupId, applicantId);
            if (existingApplication is not null)
                return new GroupApplicationResult(
                    GroupApplicationOutcome.GroupApplicationCreated,
                    await MapToDtoAsync(existingApplication));

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
                if (await _applicationRepo.GetPendingForUserAsync(groupId, applicantId) is not null)
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

            var application = await _applicationRepo.GetPendingForUserAsync(groupId, applicantId)
                ?? throw new InvalidOperationException("Group application was not created.");
            return new GroupApplicationResult(
                GroupApplicationOutcome.GroupApplicationCreated,
                await MapToDtoAsync(application));
        }

        private static bool IsGroupApplicationUniqueViolation(DbUpdateException exception) =>
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

        public async Task ApproveAsync(long applicationId)
        {
            var callerId = GetUserIdFromLogin();
            var application = await GetApplicationOrThrowAsync(applicationId);
            await _membershipService.EnsureAdminOrOwnerAsync(application.GroupId, callerId);

            if (await _membershipService.IsMemberAsync(application.GroupId, application.ApplicantId))
                return;

            await _joinResolution.CreateMembershipFromJoinRequestsAsync(application.GroupId, application.ApplicantId);
        }

        public async Task RejectAsync(long applicationId)
        {
            var callerId = GetUserIdFromLogin();
            var application = await GetApplicationOrThrowAsync(applicationId);
            await _membershipService.EnsureAdminOrOwnerAsync(application.GroupId, callerId);

            await _applicationRepo.DeleteApplicationAsync(application);
        }

        public async Task CancelAsync(long applicationId)
        {
            var userId = GetUserIdFromLogin();
            var application = await GetApplicationOrThrowAsync(applicationId);
            if (application.ApplicantId != userId)
                throw new UnauthorizedAccessException("Only the applicant can cancel this application.");

            await _applicationRepo.DeleteApplicationAsync(application);
        }

        public async Task<List<GroupApplicationResponseDto>> GetPendingByGroupAsync(long groupId)
        {
            var callerId = GetUserIdFromLogin();
            await _membershipService.EnsureAdminOrOwnerAsync(groupId, callerId);

            var applications = await _applicationRepo.GetPendingByGroupAsync(groupId);
            if (applications.Count == 0) return new List<GroupApplicationResponseDto>();

            var nicknames = await _userService.GetNicknamesByUserIdsAsync(applications.Select(a => a.ApplicantId));
            return applications
                .Select(a => Map(a, nicknames.GetValueOrDefault(a.ApplicantId, "Deleted User")))
                .ToList();
        }

        public Task DeleteAllByGroupAsync(long groupId) => _applicationRepo.DeleteAllByGroupAsync(groupId);

        public Task DeleteAllByUserAsync(long userId) => _applicationRepo.DeleteAllByUserAsync(userId);

        private async Task<GroupApplicationResponseDto> MapToDtoAsync(GroupApplication application)
        {
            var nickname = (await _userService.GetUserByIdAsync(application.ApplicantId))?.Nickname ?? "Deleted User";
            return Map(application, nickname);
        }

        private static GroupApplicationResponseDto Map(GroupApplication application, string nickname) => new(
            Id: application.Id,
            GroupId: application.GroupId,
            ApplicantId: application.ApplicantId,
            ApplicantNickname: nickname,
            IsPending: application.IsPending,
            CreatedAt: application.CreatedAt,
            UpdatedAt: application.UpdatedAt);
    }
}
