using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Repository;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupBoardService
    {
        private readonly IGroupBoardRepository _boardRepo;
        private readonly IGroupRepository _groupRepo;
        private readonly GroupMembershipService _membership;
        private readonly GroupBoardAccessService _access;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GroupBoardService(
            IGroupBoardRepository boardRepo,
            IGroupRepository groupRepo,
            GroupMembershipService membership,
            GroupBoardAccessService access,
            IHttpContextAccessor httpContextAccessor)
        {
            _boardRepo = boardRepo;
            _groupRepo = groupRepo;
            _membership = membership;
            _access = access;
            _httpContextAccessor = httpContextAccessor;
        }

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new EntityNotFoundException("Unauthorized Access"));

        public async Task<List<GroupBoardResponseDto>> ListAsync(long groupId)
        {
            var group = await _groupRepo.GetGroupByIdAsync(groupId)
                ?? throw new EntityNotFoundException("Group not found");

            var boards = await _boardRepo.GetByGroupAsync(groupId);
            var visible = new List<GroupBoardResponseDto>();
            foreach (var board in boards)
            {
                if (!await _access.TryCanViewBoardAsync(groupId, board.Id))
                    continue;
                visible.Add(MapToDto(board));
            }

            return visible;
        }

        public async Task<GroupBoardResponseDto> CreateAsync(long groupId, GroupBoardCreateRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureAdminOrOwnerAsync(groupId, callerId);

            var group = await _groupRepo.GetGroupByIdAsync(groupId)
                ?? throw new EntityNotFoundException("Group not found");

            if (await _boardRepo.ExistsByNameAsync(groupId, request.Name))
                throw new EntityAlreadyExistsException("A board with this name already exists in the group.");

            var visibility = request.Visibility
                ?? (group.Visibility == GroupVisibility.Public ? BoardVisibility.ForAll : BoardVisibility.MembersOnly);
            var board = new GroupBoard(groupId, request.Name, visibility, request.Description);
            await _boardRepo.CreateAsync(board);

            return MapToDto(board);
        }

        public async Task<GroupBoardResponseDto> UpdateAsync(long groupId, long boardId, GroupBoardPatchRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureAdminOrOwnerAsync(groupId, callerId);

            var group = await _groupRepo.GetGroupByIdAsync(groupId)
                ?? throw new EntityNotFoundException("Group not found");

            var board = await _boardRepo.GetByGroupAndIdAsync(groupId, boardId)
                ?? throw new EntityNotFoundException("Board not found");

            if (await _boardRepo.ExistsByNameAsync(groupId, request.Name, boardId))
                throw new EntityAlreadyExistsException("A board with this name already exists in the group.");

            board.Update(request.Name, request.Visibility, request.Description);
            await _boardRepo.UpdateAsync(board);

            return MapToDto(board);
        }

        public async Task DeleteAsync(long groupId, long boardId)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureAdminOrOwnerAsync(groupId, callerId);

            var board = await _boardRepo.GetByGroupAndIdAsync(groupId, boardId)
                ?? throw new EntityNotFoundException("Board not found");

            await _boardRepo.DeleteAsync(board);
        }

        private static GroupBoardResponseDto MapToDto(GroupBoard board) => new(
            Id: board.Id,
            GroupId: board.GroupId,
            Name: board.Name,
            Description: board.Description,
            Visibility: board.Visibility,
            CreatedAt: board.CreatedAt,
            UpdatedAt: board.UpdatedAt);
    }
}
