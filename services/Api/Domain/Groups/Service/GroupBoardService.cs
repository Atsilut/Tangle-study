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

        public async Task<List<GroupBoardGetResponseDto>?> ListAsync(long groupId)
        {
            await _groupService.Value.EnsureGroupExistsAsync(groupId);

            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);
            var boards = await _repo.GetByGroupAsync(groupId);
            var visible = await _access.FilterViewableBoardsAsync(groupId, boards);
            if (visible.Count == 0) return null;

            var (userId, member) = await _access.GetViewerContextForGroupAsync(group);
            return [.. visible.Select(board => MapToDto(group, board, userId, member))];
        }

        public async Task<GroupBoardGetResponseDto> CreateAsync(long groupId, GroupBoardCreateRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureAdminOrOwnerAsync(groupId, callerId);

            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            if (await _repo.ExistsByNameAsync(groupId, request.Name)) throw new EntityAlreadyExistsException("A board with this name already exists in the group.");

            var visibility = request.Visibility
                ?? (group.Visibility == GroupVisibility.Public ? BoardVisibility.ForAll : BoardVisibility.MembersOnly);
            var writeability = request.Writeability ?? BoardWriteability.MembersOnly;
            var board = new GroupBoard(groupId, request.Name, visibility, request.Description, writeability);
            await _repo.CreateAsync(board);

            return await MapToDtoAsync(group, board);
        }

        public async Task<GroupBoardGetResponseDto> UpdateAsync(long groupId, long boardId, GroupBoardPatchRequestDto request)
        {
            var callerId = GetUserIdFromLogin();
            await _membership.EnsureAdminOrOwnerAsync(groupId, callerId);

            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            var board = await _repo.GetByGroupAndIdAsync(groupId, boardId)
                ?? throw new EntityNotFoundException("Board not found");

            if (await _repo.ExistsByNameAsync(groupId, request.Name, boardId)) throw new EntityAlreadyExistsException("A board with this name already exists in the group.");

            board.Update(request.Name, request.Visibility, request.Writeability, request.Description);
            await _repo.UpdateAsync(board);

            return await MapToDtoAsync(group, board);
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

        private async Task<GroupBoardGetResponseDto> MapToDtoAsync(Group group, GroupBoard board)
        {
            var (userId, member) = await _access.GetViewerContextForGroupAsync(group);
            return MapToDto(group, board, userId, member);
        }

        private GroupBoardGetResponseDto MapToDto(Group group, GroupBoard board, long? userId, GroupMember? member) => new(
            Id: board.Id,
            GroupId: board.GroupId,
            Name: board.Name,
            Description: board.Description,
            Visibility: board.Visibility,
            Writeability: board.Writeability,
            CanWrite: _access.CanWriteBoardForViewer(group, board, userId, member),
            CreatedAt: board.CreatedAt,
            UpdatedAt: board.UpdatedAt);
    }
}
