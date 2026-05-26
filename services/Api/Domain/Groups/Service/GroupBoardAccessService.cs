using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Repository;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupBoardAccessService
    {
        private readonly IGroupRepository _groupRepo;
        private readonly IGroupBoardRepository _boardRepo;
        private readonly IGroupMemberRepository _memberRepo;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GroupBoardAccessService(
            IGroupRepository groupRepo,
            IGroupBoardRepository boardRepo,
            IGroupMemberRepository memberRepo,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepo = groupRepo;
            _boardRepo = boardRepo;
            _memberRepo = memberRepo;
            _httpContextAccessor = httpContextAccessor;
        }

        private long? TryGetUserIdFromLogin()
        {
            var sub = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
            return long.TryParse(sub, out var id) ? id : null;
        }

        public async Task<GroupBoard> GetBoardOrThrowAsync(long groupId, long boardId, string notFoundMessage = "Board not found")
        {
            if (await _groupRepo.GetGroupByIdAsync(groupId) is null)
                throw new EntityNotFoundException("Group not found");

            var board = await _boardRepo.GetByGroupAndIdAsync(groupId, boardId);
            if (board is null) throw new EntityNotFoundException(notFoundMessage);
            return board;
        }

        public async Task<bool> TryCanViewBoardAsync(long groupId, long boardId)
        {
            var group = await _groupRepo.GetGroupByIdAsync(groupId);
            if (group is null) throw new EntityNotFoundException("Group not found");

            var board = await _boardRepo.GetByGroupAndIdAsync(groupId, boardId);
            if (board is null) return false;

            return await CanViewBoardInternalAsync(group, board);
        }

        public async Task EnsureCanViewBoardAsync(long groupId, long boardId)
        {
            var group = await _groupRepo.GetGroupByIdAsync(groupId)
                ?? throw new EntityNotFoundException("Group not found");

            var board = await _boardRepo.GetByGroupAndIdAsync(groupId, boardId)
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

            var member = await _memberRepo.GetMemberAsync(group.Id, userId.Value);
            if (member is null) return false;

            if (board.Visibility == BoardVisibility.MembersOnly)
                return true;

            return board.Visibility == BoardVisibility.AdminOnly
                   && (member.Role == GroupRole.Admin || member.Role == GroupRole.Owner);
        }

        public Task EnsureCanWritePostAsync(long groupId, long boardId) =>
            EnsureCanViewBoardAsync(groupId, boardId);
    }
}

