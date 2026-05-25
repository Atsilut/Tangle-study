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
        private readonly IGroupBlacklistRepository _blacklistRepo;
        private readonly GroupMembershipService _membershipService;
        private readonly AppDbContext _db;

        public GroupJoinResolutionService(
            IGroupInvitationRepository invitationRepo,
            IGroupApplicationRepository applicationRepo,
            IGroupBlacklistRepository blacklistRepo,
            GroupMembershipService membershipService,
            AppDbContext db)
        {
            _invitationRepo = invitationRepo;
            _applicationRepo = applicationRepo;
            _blacklistRepo = blacklistRepo;
            _membershipService = membershipService;
            _db = db;
        }

        public async Task CreateMembershipFromJoinRequestsAsync(long groupId, long userId)
        {
            if (await _blacklistRepo.ExistsAsync(groupId, userId))
                throw new ArgumentException("This user is blacklisted from the group.");

            await _db.ExecuteInTransactionAsync(async () =>
            {
                await DeleteJoinArtifactsForUserAndGroupAsync(groupId, userId);
                await _membershipService.AddMemberInternalAsync(groupId, userId, GroupRole.Member);
            });
        }

        public Task DeleteJoinArtifactsForUserAndGroupAsync(long groupId, long userId) =>
            _db.ExecuteInTransactionAsync(async () =>
            {
                await _invitationRepo.DeleteAllForUserAndGroupAsync(groupId, userId);
                await _applicationRepo.DeleteAllForUserAndGroupAsync(groupId, userId);
            });
    }
}
