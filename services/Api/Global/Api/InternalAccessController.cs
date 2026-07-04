using Api.Domain.Friendships.Service;
using Api.Domain.Groups.Service;
using Api.Domain.UserBlocks.Service;
using Api.Domain.Users.Service;
using Api.Global.Dto;
using Api.Global.Security;
using Microsoft.AspNetCore.Mvc;

namespace Api.Global.Api;

[ApiController]
[Route("internal/access")]
[ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
public sealed class InternalAccessController(
    UserService userService,
    FriendshipService friendshipService,
    UserBlockService userBlockService,
    GroupService groupService,
    GroupMembershipService groupMembershipService,
    GroupBoardAccessService groupBoardAccessService) : ControllerBase
{
    [HttpPost("users/{userId:long}/exists")]
    public async Task<IActionResult> EnsureUserExists([FromRoute] long userId)
    {
        await userService.EnsureUserExistsAsync(userId, "Authentication failed", StatusCodes.Status400BadRequest);
        return NoContent();
    }

    [HttpPost("users/{userId:long}/validate")]
    public async Task<IActionResult> ValidateUserExists([FromRoute] long userId)
    {
        await userService.EnsureUserExistsAsync(userId, "User not found", StatusCodes.Status400BadRequest);
        return NoContent();
    }

    [HttpPost("users/validate-exist")]
    public async Task<IActionResult> EnsureUsersExist([FromBody] InternalAccessUserIdsRequestDto request)
    {
        await userService.EnsureUsersExistAsync(
            request.UserIds,
            "User not found",
            StatusCodes.Status400BadRequest);
        return NoContent();
    }

    [HttpPost("users/nicknames")]
    public async Task<ActionResult<InternalAccessNicknamesResponseDto>> GetNicknames(
        [FromBody] InternalAccessUserIdsRequestDto request)
    {
        var nicknames = await userService.GetNicknamesByUserIdsAsync(request.UserIds);
        return Ok(new InternalAccessNicknamesResponseDto(
            [.. request.UserIds
                .Distinct()
                .Select(id => new InternalAccessNicknameEntryDto(id, nicknames.GetValueOrDefault(id, "Deleted User")))]));
    }

    [HttpPost("users/by-nickname")]
    public async Task<ActionResult<InternalAccessNicknameLookupResponseDto>> GetUserIdByNickname(
        [FromBody] InternalAccessNicknameLookupRequestDto request)
    {
        var user = await userService.GetUserByNicknameAsync(request.Nickname);
        if (user is null) return NotFound();
        return Ok(new InternalAccessNicknameLookupResponseDto(user.Id));
    }

    [HttpPost("friendships/validate-pair")]
    public async Task<IActionResult> ValidateFriendshipPair([FromBody] InternalAccessOtherUserRequestDto request)
    {
        var callerId = GetCallerUserId();
        await friendshipService.EnsureFriendshipExistsForUserPairAsync(callerId, request.OtherUserId);
        return NoContent();
    }

    [HttpPost("users/blocks/validate-between")]
    public async Task<IActionResult> ValidateNoBlockBetweenUsers([FromBody] InternalAccessOtherUserRequestDto request)
    {
        var callerId = GetCallerUserId();
        if (await userBlockService.IsBlockedByAsync(callerId, request.OtherUserId)
            || await userBlockService.IsBlockedByAsync(request.OtherUserId, callerId))
            throw new ArgumentException("Cannot chat while a block exists between you and this user.");

        return NoContent();
    }

    [HttpPost("users/blocks/validate-against-others")]
    public async Task<IActionResult> ValidateNoBlockAgainstOthers([FromBody] InternalAccessUserIdsRequestDto request)
    {
        var callerId = GetCallerUserId();
        await userBlockService.EnsureNoBlockBetweenUserAndOthersAsync(callerId, request.UserIds);
        return NoContent();
    }

    [HttpPost("groups/{groupId:long}/exists")]
    public async Task<IActionResult> EnsureGroupExists([FromRoute] long groupId)
    {
        await groupService.EnsureGroupExistsAsync(groupId);
        return NoContent();
    }

    [HttpPost("groups/{groupId:long}/members/validate")]
    public async Task<IActionResult> ValidateGroupMembers(
        [FromRoute] long groupId,
        [FromBody] InternalAccessGroupMembersRequestDto request)
    {
        await groupMembershipService.EnsureMembersAsync(
            groupId,
            request.UserIds,
            "All participants must be members of this group");
        return NoContent();
    }

    [HttpPost("groups/{groupId:long}/members/{userId:long}/validate")]
    public async Task<IActionResult> ValidateGroupMember([FromRoute] long groupId, [FromRoute] long userId)
    {
        await groupMembershipService.EnsureMemberAsync(
            groupId,
            userId,
            "User is not a member of this group");
        return NoContent();
    }

    [HttpGet("groups/{groupId:long}/membership/me")]
    public async Task<IActionResult> EnsureCallerIsGroupMember([FromRoute] long groupId)
    {
        var callerId = GetCallerUserId();
        await groupMembershipService.EnsureMemberAsync(groupId, callerId);
        return NoContent();
    }

    [HttpPost("groups/{groupId:long}/boards/{boardId:long}/validate-view")]
    public async Task<IActionResult> ValidateBoardView([FromRoute] long groupId, [FromRoute] long boardId)
    {
        await groupBoardAccessService.EnsureCanViewBoardAsync(groupId, boardId);
        return NoContent();
    }

    [HttpPost("groups/{groupId:long}/boards/{boardId:long}/validate-write")]
    public async Task<IActionResult> ValidateBoardWrite([FromRoute] long groupId, [FromRoute] long boardId)
    {
        await groupBoardAccessService.EnsureCanWritePostAsync(groupId, boardId);
        return NoContent();
    }

    [HttpPost("groups/boards/viewable-keys")]
    public async Task<ActionResult<InternalAccessViewableBoardsResponseDto>> GetViewableBoardKeys(
        [FromBody] InternalAccessViewableBoardsRequestDto request)
    {
        var keys = request.Boards.Select(b => (b.GroupId, b.BoardId)).ToList();
        var viewable = await groupBoardAccessService.ResolveViewableBoardKeysAsync(keys);
        return Ok(new InternalAccessViewableBoardsResponseDto(
            [.. viewable.Select(k => new InternalAccessBoardKeyDto(k.GroupId, k.BoardId))]));
    }

    [HttpPost("users/blocks/mutual-ids")]
    public async Task<ActionResult<InternalAccessMutualBlocksResponseDto>> GetMutualBlockIds(
        [FromBody] InternalAccessMutualBlocksRequestDto request)
    {
        var blocked = await userBlockService.GetMutuallyBlockedUserIdsAsync(request.UserId, request.OtherUserIds);
        return Ok(new InternalAccessMutualBlocksResponseDto([.. blocked]));
    }

    [HttpGet("groups/{groupId:long}/members/for-member")]
    public async Task<ActionResult<InternalAccessGroupMembersResponseDto>> GetGroupMembersForMember(
        [FromRoute] long groupId)
    {
        var callerId = GetCallerUserId();
        var members = await groupMembershipService.GetMembersForMemberAsync(groupId, callerId);
        return Ok(new InternalAccessGroupMembersResponseDto(
            [.. members.Select(m => new InternalAccessGroupMemberEntryDto(m.UserId, m.Nickname))]));
    }

    [HttpGet("groups/{groupId:long}/member-ids")]
    public async Task<ActionResult<InternalAccessGroupMemberIdsResponseDto>> GetGroupMemberIds(
        [FromRoute] long groupId)
    {
        await groupService.EnsureGroupExistsAsync(groupId);
        var memberIds = await groupMembershipService.GetMemberUserIdsAsync(groupId);
        return Ok(new InternalAccessGroupMemberIdsResponseDto([.. memberIds]));
    }

    private long GetCallerUserId() =>
        long.Parse(User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));
}
