using Api.Domain.Groups.Domain;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupJoinService
    {
        private readonly Lazy<GroupService> _groupService;
        private readonly GroupInvitationService _groupInvitationService;
        private readonly GroupMembershipService _membershipService;
        private readonly GroupJoinResolutionService _joinResolution;
        private readonly GroupBlacklistService _blacklistService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GroupJoinService(
            Lazy<GroupService> groupService,
            GroupInvitationService groupInvitationService,
            GroupMembershipService membershipService,
            GroupJoinResolutionService joinResolution,
            GroupBlacklistService blacklistService,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupService = groupService;
            _groupInvitationService = groupInvitationService;
            _membershipService = membershipService;
            _joinResolution = joinResolution;
            _blacklistService = blacklistService;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

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
