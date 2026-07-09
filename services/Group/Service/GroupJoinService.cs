using Tangle.AspNetCore.Auth;
using Tangle.AspNetCore.Exceptions;
using Group.Infrastructure;

namespace Group.Service
{
    [Service]
    public class GroupJoinService(
        Lazy<GroupService> groupService,
        GroupInvitationService groupInvitationService,
        GroupMembershipService membershipService,
        GroupJoinResolutionService joinResolution,
        GroupBlacklistService blacklistService,
        CurrentUserAccessor currentUser)
    {
        private readonly Lazy<GroupService> _groupService = groupService;
        private readonly GroupInvitationService _groupInvitationService = groupInvitationService;
        private readonly GroupMembershipService _membershipService = membershipService;
        private readonly GroupJoinResolutionService _joinResolution = joinResolution;
        private readonly GroupBlacklistService _blacklistService = blacklistService;
        private readonly CurrentUserAccessor _currentUser = currentUser;

        private long GetUserIdFromLogin() => _currentUser.GetUserIdFromLogin();

        public async Task JoinAsync(long groupId)
        {
            var userId = GetUserIdFromLogin();
            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            GroupJoinPolicyRules.EnsureCanOpenJoin(group.JoinPolicy);

            if (await _membershipService.IsMemberAsync(groupId, userId))
                throw new EntityAlreadyExistsException("You are already a member of this group.");

            await _blacklistService.EnsureNotBlacklistedAsync(groupId, userId);

            var pendingInvitation = await _groupInvitationService.GetPendingForUserAsync(groupId, userId);
            if (pendingInvitation is not null)
            {
                await _joinResolution.CreateMembershipFromJoinRequestsAsync(groupId, userId);
                return;
            }

            await _joinResolution.CreateMembershipFromJoinRequestsAsync(groupId, userId);
        }
    }
}
