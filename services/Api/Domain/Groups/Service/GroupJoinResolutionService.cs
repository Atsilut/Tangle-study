using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Repository;
using Api.Global.Db;
using Api.Global.Infrastructure;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupJoinResolutionService
    {
        private readonly IGroupInvitationRepository _invitationRepo;
        private readonly IGroupApplicationRepository _applicationRepo;
        private readonly GroupMembershipService _membershipService;
        private readonly AppDbContext _db;

        public GroupJoinResolutionService(
            IGroupInvitationRepository invitationRepo,
            IGroupApplicationRepository applicationRepo,
            GroupMembershipService membershipService,
            AppDbContext db)
        {
            _invitationRepo = invitationRepo;
            _applicationRepo = applicationRepo;
            _membershipService = membershipService;
            _db = db;
        }

        public Task CreateMembershipFromJoinRequestsAsync(long groupId, long userId) =>
            _db.ExecuteInTransactionAsync(async () =>
            {
                await DeleteJoinArtifactsForUserAndGroupAsync(groupId, userId);
                await _membershipService.AddMemberInternalAsync(groupId, userId, GroupRole.Member);
            });

        public Task DeleteJoinArtifactsForUserAndGroupAsync(long groupId, long userId) =>
            _db.ExecuteInTransactionAsync(async () =>
            {
                await _invitationRepo.DeleteAllForUserAndGroupAsync(groupId, userId);
                await _applicationRepo.DeleteAllForUserAndGroupAsync(groupId, userId);
            });
    }
}
