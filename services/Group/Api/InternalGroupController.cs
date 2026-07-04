using Microsoft.AspNetCore.Mvc;
using Group.Security;
using Group.Service;

namespace Group.Api;

[ApiController]
[Route("internal/group")]
[ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
public sealed class InternalGroupController(
    GroupService groupService,
    GroupMembershipService groupMembershipService,
    GroupBoardAccessService groupBoardAccessService) : ControllerBase
{
    [HttpPost("{groupId:long}/exists")]
    public async Task<IActionResult> EnsureGroupExists([FromRoute] long groupId)
    {
        await groupService.EnsureGroupExistsAsync(groupId);
        return NoContent();
    }

    [HttpPost("{groupId:long}/members/validate")]
    public async Task<IActionResult> ValidateGroupMembers(
        [FromRoute] long groupId,
        [FromBody] InternalGroupMembersRequestDto request)
    {
        await groupMembershipService.EnsureMembersAsync(
            groupId,
            request.UserIds,
            "All participants must be members of this group");
        return NoContent();
    }

    [HttpPost("{groupId:long}/members/{userId:long}/validate")]
    public async Task<IActionResult> ValidateGroupMember([FromRoute] long groupId, [FromRoute] long userId)
    {
        await groupMembershipService.EnsureMemberAsync(
            groupId,
            userId,
            "User is not a member of this group");
        return NoContent();
    }

    [HttpGet("{groupId:long}/membership/me")]
    public async Task<IActionResult> EnsureCallerIsGroupMember([FromRoute] long groupId)
    {
        var callerId = GetCallerUserId();
        await groupMembershipService.EnsureMemberAsync(groupId, callerId);
        return NoContent();
    }

    [HttpPost("{groupId:long}/boards/{boardId:long}/validate-view")]
    public async Task<IActionResult> ValidateBoardView([FromRoute] long groupId, [FromRoute] long boardId)
    {
        await groupBoardAccessService.EnsureCanViewBoardAsync(groupId, boardId);
        return NoContent();
    }

    [HttpPost("{groupId:long}/boards/{boardId:long}/validate-write")]
    public async Task<IActionResult> ValidateBoardWrite([FromRoute] long groupId, [FromRoute] long boardId)
    {
        await groupBoardAccessService.EnsureCanWritePostAsync(groupId, boardId);
        return NoContent();
    }

    [HttpPost("boards/viewable-keys")]
    public async Task<ActionResult<InternalGroupViewableBoardsResponseDto>> GetViewableBoardKeys(
        [FromBody] InternalGroupViewableBoardsRequestDto request)
    {
        var keys = request.Boards.Select(b => (b.GroupId, b.BoardId)).ToList();
        var viewable = await groupBoardAccessService.ResolveViewableBoardKeysAsync(keys);
        return Ok(new InternalGroupViewableBoardsResponseDto(
            [.. viewable.Select(k => new InternalGroupBoardKeyDto(k.GroupId, k.BoardId))]));
    }

    [HttpGet("{groupId:long}/members/for-member")]
    public async Task<ActionResult<InternalGroupMembersResponseDto>> GetGroupMembersForMember(
        [FromRoute] long groupId)
    {
        var callerId = GetCallerUserId();
        var members = await groupMembershipService.GetMembersForMemberAsync(groupId, callerId);
        return Ok(new InternalGroupMembersResponseDto(
            [.. members.Select(m => new InternalGroupMemberEntryDto(m.UserId, m.Nickname))]));
    }

    [HttpGet("{groupId:long}/member-ids")]
    public async Task<ActionResult<InternalGroupMemberIdsResponseDto>> GetGroupMemberIds(
        [FromRoute] long groupId)
    {
        await groupService.EnsureGroupExistsAsync(groupId);
        var memberIds = await groupMembershipService.GetMemberUserIdsAsync(groupId);
        return Ok(new InternalGroupMemberIdsResponseDto([.. memberIds]));
    }

    [HttpPost("users/{userId:long}/detach-on-deletion")]
    public async Task<IActionResult> DetachOnDeletion([FromRoute] long userId)
    {
        await groupMembershipService.HandleUserDeletionAsync(userId);
        return NoContent();
    }

    private long GetCallerUserId() =>
        long.Parse(User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));
}

public sealed record InternalGroupMembersRequestDto(long[] UserIds);

public sealed record InternalGroupBoardKeyDto(long GroupId, long BoardId);

public sealed record InternalGroupViewableBoardsRequestDto(InternalGroupBoardKeyDto[] Boards);

public sealed record InternalGroupViewableBoardsResponseDto(InternalGroupBoardKeyDto[] Viewable);

public sealed record InternalGroupMemberEntryDto(long UserId, string Nickname);

public sealed record InternalGroupMembersResponseDto(IReadOnlyList<InternalGroupMemberEntryDto> Members);

public sealed record InternalGroupMemberIdsResponseDto(long[] MemberUserIds);
