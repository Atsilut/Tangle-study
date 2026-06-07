using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Repository;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupBoardAccessService(
        IGroupBoardRepository repo,
        Lazy<GroupService> groupService,
        GroupMembershipService membershipService,
        IHttpContextAccessor httpContextAccessor)
    {
        private readonly IGroupBoardRepository _repo = repo;
        private readonly Lazy<GroupService> _groupService = groupService;
        private readonly GroupMembershipService _membershipService = membershipService;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        private long? TryGetUserIdFromLogin()
        {
            var sub = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
            return long.TryParse(sub, out var id) ? id : null;
        }

        public async Task<GroupBoard> GetBoardOrThrowAsync(long groupId, long boardId, string notFoundMessage = "Board not found")
        {
            await _groupService.Value.EnsureGroupExistsAsync(groupId);

            var board = await _repo.GetByGroupAndIdAsync(groupId, boardId);
            if (board is null) throw new EntityNotFoundException(notFoundMessage);
            return board;
        }

        public async Task<bool> TryCanViewBoardAsync(long groupId, long boardId)
        {
            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            var board = await _repo.GetByGroupAndIdAsync(groupId, boardId);
            if (board is null) return false;

            return await CanViewBoardInternalAsync(group, board);
        }

        public async Task EnsureCanViewBoardAsync(long groupId, long boardId)
        {
            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            var board = await _repo.GetByGroupAndIdAsync(groupId, boardId)
                ?? throw new EntityNotFoundException("Board not found");

            if (!await CanViewBoardInternalAsync(group, board))
                throw new UnauthorizedAccessException("Unauthorized access");
        }

        private async Task<bool> CanViewBoardInternalAsync(Group group, GroupBoard board)
        {
            if (board.Visibility == BoardVisibility.ForAll && group.Visibility == GroupVisibility.Public)
                return true;

            var userId = TryGetUserIdFromLogin();
            if (userId is null) return false;

            var member = await _membershipService.GetMemberAsync(group.Id, userId.Value);
            if (member is null) return false;

            if (board.Visibility is BoardVisibility.MembersOnly or BoardVisibility.ForAll)
                return true;

            return member.Role is GroupRole.Admin or GroupRole.Owner;
        }

        public Task EnsureCanWritePostAsync(long groupId, long boardId) =>
            EnsureCanViewBoardAsync(groupId, boardId);
    }
}
