using Api.Domain.Chat.Service;
using Api.Domain.Comments.Service;
using Api.Domain.Friendships.Service;
using Api.Domain.Groups.Service;
using Api.Domain.Posts.Service;
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
    PostService postService,
    CommentService commentService,
    ChatMessageService chatMessageService,
    FriendshipService friendshipService,
    UserBlockService userBlockService,
    GroupService groupService,
    GroupMembershipService groupMembershipService) : ControllerBase
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

    [HttpPost("posts/{postId:long}/media-view")]
    public async Task<IActionResult> EnsureCanViewPostMedia([FromRoute] long postId)
    {
        await postService.EnsureCanViewPostMediaAsync(postId);
        return NoContent();
    }

    [HttpPost("comments/{commentId:long}/media-view")]
    public async Task<IActionResult> EnsureCanViewCommentMedia([FromRoute] long commentId)
    {
        await commentService.EnsureCanViewCommentMediaAsync(commentId);
        return NoContent();
    }

    [HttpPost("chat-messages/{chatMessageId:long}/media-view")]
    public async Task<IActionResult> EnsureCanViewChatMessageMedia([FromRoute] long chatMessageId)
    {
        await chatMessageService.EnsureCurrentUserCanAccessMessageAsync(chatMessageId);
        return NoContent();
    }

    private long GetCallerUserId() =>
        long.Parse(User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));
}
