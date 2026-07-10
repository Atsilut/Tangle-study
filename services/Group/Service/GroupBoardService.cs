using Group.Entities;
using Group.Dto;
using Group.Repository;
using Tangle.AspNetCore.Auth;
using Tangle.AspNetCore.Exceptions;
using Group.Infrastructure;
using GroupEntity = Group.Entities.Group;

namespace Group.Service
{
    [Service]
    public class GroupBoardService(
        IGroupBoardRepository repo,
        Lazy<GroupService> groupService,
        GroupMembershipService membership,
        CurrentUserAccessor currentUser)
    {
        private readonly IGroupBoardRepository _repo = repo;
        private readonly Lazy<GroupService> _groupService = groupService;
        private readonly GroupMembershipService _membership = membership;
        private readonly CurrentUserAccessor _currentUser = currentUser;

        public async Task<List<GroupBoardGetResponseDto>?> ListAsync(long groupId)
        {
            await _groupService.Value.EnsureGroupExistsAsync(groupId);

            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);
            var boards = await _repo.GetByGroupAsync(groupId);
            var visible = await FilterViewableBoardsAsync(groupId, boards);
            if (visible.Count == 0) return null;

            var (userId, member) = await GetViewerContextForGroupAsync(group);
            return [.. visible.Select(board => MapToDto(group, board, userId, member))];
        }

        public async Task<GroupBoardGetResponseDto> CreateAsync(long groupId, GroupBoardCreateRequestDto request)
        {
            var callerId = _currentUser.GetUserIdFromLogin();
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
            var callerId = _currentUser.GetUserIdFromLogin();
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
            var callerId = _currentUser.GetUserIdFromLogin();
            await _membership.EnsureAdminOrOwnerAsync(groupId, callerId);

            var board = await _repo.GetByGroupAndIdAsync(groupId, boardId)
                ?? throw new EntityNotFoundException("Board not found");

            await _repo.DeleteAsync(board);
        }

        public Task DeleteAllByGroupAsync(long groupId) => _repo.DeleteAllByGroupAsync(groupId);

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

        public async Task EnsureCanWritePostAsync(long groupId, long boardId)
        {
            var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);

            var board = await _repo.GetByGroupAndIdAsync(groupId, boardId)
                ?? throw new EntityNotFoundException("Board not found");

            var (userId, member) = await GetViewerContextAsync(group);
            if (!CanWriteBoard(group, board, userId, member))
                throw new UnauthorizedAccessException("Unauthorized access");
        }

        public async Task<(long? UserId, GroupMember? Member)> GetViewerContextForGroupAsync(GroupEntity group) =>
            await GetViewerContextAsync(group);

        public bool CanWriteBoardForViewer(GroupEntity group, GroupBoard board, long? userId, GroupMember? member) =>
            CanWriteBoard(group, board, userId, member);

        public async Task<HashSet<(long GroupId, long BoardId)>> ResolveViewableBoardKeysAsync(
            IReadOnlyCollection<(long GroupId, long BoardId)> boardKeys)
        {
            if (boardKeys.Count == 0) return [];

            var viewable = new HashSet<(long GroupId, long BoardId)>();
            foreach (var groupKeys in boardKeys.GroupBy(k => k.GroupId))
            {
                var groupId = groupKeys.Key;
                var group = await _groupService.Value.GetGroupOrThrowAsync(groupId);
                var (userId, member) = await GetViewerContextAsync(group);
                var boardIds = groupKeys.Select(k => k.BoardId).Distinct().ToList();
                var boards = await _repo.GetByGroupAndIdsAsync(groupId, boardIds);
                var boardsById = boards.ToDictionary(b => b.Id);

                foreach (var boardId in boardIds)
                {
                    if (!boardsById.TryGetValue(boardId, out var board)) continue;
                    if (CanViewBoard(group, board, userId, member))
                        viewable.Add((groupId, boardId));
                }
            }

            return viewable;
        }

        public async Task<bool> CanWriteBoardForViewerAsync(GroupEntity group, GroupBoard board)
        {
            var (userId, member) = await GetViewerContextAsync(group);
            return CanWriteBoard(group, board, userId, member);
        }

        private async Task<GroupBoardGetResponseDto> MapToDtoAsync(GroupEntity group, GroupBoard board)
        {
            var (userId, member) = await GetViewerContextForGroupAsync(group);
            return MapToDto(group, board, userId, member);
        }

        private GroupBoardGetResponseDto MapToDto(GroupEntity group, GroupBoard board, long? userId, GroupMember? member) => new(
            Id: board.Id,
            GroupId: board.GroupId,
            Name: board.Name,
            Description: board.Description,
            Visibility: board.Visibility,
            Writeability: board.Writeability,
            CanWrite: CanWriteBoardForViewer(group, board, userId, member),
            CreatedAt: board.CreatedAt,
            UpdatedAt: board.UpdatedAt);

        private async Task<(long? UserId, GroupMember? Member)> GetViewerContextAsync(GroupEntity group)
        {
            var userId = _currentUser.TryGetUserIdFromLogin();
            if (userId is null) return (null, null);

            var member = await _membership.GetMemberAsync(group.Id, userId.Value);
            return (userId, member);
        }

        private static bool CanViewBoard(GroupEntity group, GroupBoard board, long? userId, GroupMember? member)
        {
            if (board.Visibility == BoardVisibility.ForAll && group.Visibility == GroupVisibility.Public)
                return true;

            if (userId is null || member is null) return false;

            if (board.Visibility is BoardVisibility.MembersOnly or BoardVisibility.ForAll)
                return true;

            return member.Role is GroupRole.Admin or GroupRole.Owner;
        }

        private static bool CanWriteBoard(GroupEntity group, GroupBoard board, long? userId, GroupMember? member) => board.Writeability switch
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
