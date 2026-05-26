using Api.Domain.Groups.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupJoinResolutionService
    {
        private readonly GroupMembershipService _membershipService;
        private readonly GroupInvitationService _groupInvitationService;
        private readonly GroupApplicationService _groupApplicationService;
        private readonly GroupBlacklistService _blacklistService;
        private readonly AppDbContext _db;

        public GroupJoinResolutionService(
            GroupMembershipService membershipService,
            GroupInvitationService groupInvitationService,
            GroupApplicationService groupApplicationService,
            GroupBlacklistService blacklistService,
            AppDbContext db)
        {
            _membershipService = membershipService;
            _groupInvitationService = groupInvitationService;
            _groupApplicationService = groupApplicationService;
            _blacklistService = blacklistService;
            _db = db;
        }

        public async Task CreateMembershipFromJoinRequestsAsync(long groupId, long userId)
        {
            if (await _blacklistService.IsBlacklistedAsync(groupId, userId))
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
                await _groupInvitationService.DeleteAllForUserAndGroupAsync(groupId, userId);
                await _groupApplicationService.DeleteAllForUserAndGroupAsync(groupId, userId);
            });
    }
}
