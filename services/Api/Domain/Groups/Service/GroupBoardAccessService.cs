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
            return await _repo.GetByGroupAndIdAsync(groupId, boardId) ?? throw new EntityNotFoundException(notFoundMessage);
        }

        public async Task<IReadOnlyList<GroupBoard>> FilterViewableBoardsAsync(
            long groupId,
            IReadOnlyList<GroupBoard> boards)
        {
            if (boards.Count == 0) return boards;

            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);
            var (userId, member) = await GetViewerContextAsync(group);

            List<GroupBoard> visible = [];
            foreach (var board in boards)
            {
                if (CanViewBoard(group, board, userId, member)) visible.Add(board);
            }

            return visible;
        }

        public async Task<bool> TryCanViewBoardAsync(long groupId, long boardId)
        {
            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            var board = await _repo.GetByGroupAndIdAsync(groupId, boardId);
            if (board is null) return false;

            var (userId, member) = await GetViewerContextAsync(group);
            return CanViewBoard(group, board, userId, member);
        }

        public async Task EnsureCanViewBoardAsync(long groupId, long boardId)
        {
            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            var board = await _repo.GetByGroupAndIdAsync(groupId, boardId)
                ?? throw new EntityNotFoundException("Board not found");

            var (userId, member) = await GetViewerContextAsync(group);
            if (!CanViewBoard(group, board, userId, member))
                throw new UnauthorizedAccessException("Unauthorized access");
        }

        private async Task<(long? UserId, GroupMember? Member)> GetViewerContextAsync(Group group)
        {
            var userId = TryGetUserIdFromLogin();
            if (userId is null) return (null, null);

            var member = await _membershipService.GetMemberAsync(group.Id, userId.Value);
            return (userId, member);
        }

        private static bool CanViewBoard(Group group, GroupBoard board, long? userId, GroupMember? member)
        {
            if (board.Visibility == BoardVisibility.ForAll && group.Visibility == GroupVisibility.Public)
                return true;

            if (userId is null || member is null) return false;

            if (board.Visibility is BoardVisibility.MembersOnly or BoardVisibility.ForAll)
                return true;

            return member.Role is GroupRole.Admin or GroupRole.Owner;
        }

        public async Task EnsureCanWritePostAsync(long groupId, long boardId)
        {
            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            var board = await _repo.GetByGroupAndIdAsync(groupId, boardId)
                ?? throw new EntityNotFoundException("Board not found");

            var (userId, member) = await GetViewerContextAsync(group);
            if (!CanWriteBoard(group, board, userId, member))
                throw new UnauthorizedAccessException("Unauthorized access");
        }

        public async Task<bool> CanWriteBoardForViewerAsync(Group group, GroupBoard board)
        {
            var (userId, member) = await GetViewerContextAsync(group);
            return CanWriteBoard(group, board, userId, member);
        }

        private static bool CanWriteBoard(Group group, GroupBoard board, long? userId, GroupMember? member) => board.Writeability switch
        {
            BoardWriteability.ForAll =>
                CanViewBoard(group, board, userId, member),
            BoardWriteability.MembersOnly =>
                userId is not null && member is not null && CanViewBoard(group, board, userId, member),
            BoardWriteability.AdminOnly =>
                userId is not null && member?.Role is GroupRole.Admin or GroupRole.Owner,
            _ => false,
        };
    }
}
