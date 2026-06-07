using Api.Domain.Groups.Domain;
using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Repository;
using Api.Global.Exceptions;
using Api.Global.Infrastructure;

namespace Api.Domain.Groups.Service
{
    [Service]
    public class GroupBoardService(
        IGroupBoardRepository repo,
        Lazy<GroupService> groupService,
        GroupMembershipService membership,
        GroupBoardAccessService access,
        IHttpContextAccessor httpContextAccessor)
    {
        private readonly IGroupBoardRepository _repo = repo;
        private readonly Lazy<GroupService> _groupService = groupService;
        private readonly GroupMembershipService _membership = membership;
        private readonly GroupBoardAccessService _access = access;
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        private long GetUserIdFromLogin() => long.Parse(_httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));

        public async Task<List<GroupBoardResponseDto>> ListAsync(long groupId)
        {
            await _groupService.Value.EnsureGroupExistsAsync(groupId);

            var boards = await _repo.GetByGroupAsync(groupId);
            List<GroupBoardResponseDto> visible = [];
            foreach (var board in boards)
            {
                if (!await _access.TryCanViewBoardAsync(groupId, board.Id)) continue;
                visible.Add(MapToDto(board));
            }

            return visible;
        }

        public async Task<GroupBoardResponseDto> CreateAsync(long groupId, GroupBoardCreateRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureAdminOrOwnerAsync(groupId, callerId);

            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            if (await _repo.ExistsByNameAsync(groupId, request.Name)) throw new EntityAlreadyExistsException("A board with this name already exists in the group.");

            var visibility = request.Visibility
                ?? (group.Visibility == GroupVisibility.Public ? BoardVisibility.ForAll : BoardVisibility.MembersOnly);
            var board = new GroupBoard(groupId, request.Name, visibility, request.Description);
            await _repo.CreateAsync(board);

            return MapToDto(board);
        }

        public async Task<GroupBoardResponseDto> UpdateAsync(long groupId, long boardId, GroupBoardPatchRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureAdminOrOwnerAsync(groupId, callerId);

            await _groupService.Value.EnsureGroupExistsAsync(groupId);

            var board = await _repo.GetByGroupAndIdAsync(groupId, boardId)
                ?? throw new EntityNotFoundException("Board not found");

            if (await _repo.ExistsByNameAsync(groupId, request.Name, boardId)) throw new EntityAlreadyExistsException("A board with this name already exists in the group.");

            board.Update(request.Name, request.Visibility, request.Description);
            await _repo.UpdateAsync(board);

            return MapToDto(board);
        }

        public async Task DeleteAsync(long groupId, long boardId)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureAdminOrOwnerAsync(groupId, callerId);

            var board = await _repo.GetByGroupAndIdAsync(groupId, boardId)
                ?? throw new EntityNotFoundException("Board not found");

            await _repo.DeleteAsync(board);
        }

        public Task DeleteAllByGroupAsync(long groupId) => _repo.DeleteAllByGroupAsync(groupId);

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
