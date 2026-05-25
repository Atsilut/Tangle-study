using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Repository;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupJoinService
    {
        private readonly IGroupRepository _groupRepo;
        private readonly IGroupInvitationRepository _invitationRepo;
        private readonly GroupMembershipService _membershipService;
        private readonly GroupJoinResolutionService _joinResolution;
        private readonly GroupBlacklistService _blacklistService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GroupJoinService(
            IGroupRepository groupRepo,
            IGroupInvitationRepository invitationRepo,
            GroupMembershipService membershipService,
            GroupJoinResolutionService joinResolution,
            GroupBlacklistService blacklistService,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepo = groupRepo;
            _invitationRepo = invitationRepo;
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
            var group = await _groupRepo.GetGroupByIdAsync(groupId)
                ?? throw new EntityNotFoundException("Group not found");

            GroupJoinPolicyRules.EnsureCanOpenJoin(group.JoinPolicy);

            if (await _membershipService.IsMemberAsync(groupId, userId))
                throw new EntityAlreadyExistsException("You are already a member of this group.");

            await _blacklistService.EnsureNotBlacklistedAsync(groupId, userId);

            var pendingInvitation = await _invitationRepo.GetPendingForUserAsync(groupId, userId);
            if (pendingInvitation is not null)
            {
                await _joinResolution.CreateMembershipFromJoinRequestsAsync(groupId, userId);
                return;
            }

            await _joinResolution.CreateMembershipFromJoinRequestsAsync(groupId, userId);
        }
    }
}
