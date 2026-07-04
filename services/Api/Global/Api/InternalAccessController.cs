using Api.Domain.Friendships.Service;
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
    UserBlockService userBlockService) : ControllerBase
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

    [HttpPost("users/blocks/mutual-ids")]
    public async Task<ActionResult<InternalAccessMutualBlocksResponseDto>> GetMutualBlockIds(
        [FromBody] InternalAccessMutualBlocksRequestDto request)
    {
        var blocked = await userBlockService.GetMutuallyBlockedUserIdsAsync(request.UserId, request.OtherUserIds);
        return Ok(new InternalAccessMutualBlocksResponseDto([.. blocked]));
    }

    [HttpPost("users/blocks/is-blocked-by")]
    public async Task<ActionResult<InternalAccessIsBlockedResponseDto>> IsBlockedBy(
        [FromBody] InternalAccessIsBlockedRequestDto request)
    {
        var blocked = await userBlockService.IsBlockedByAsync(request.BlockerUserId, request.BlockedUserId);
        return Ok(new InternalAccessIsBlockedResponseDto(blocked));
    }

    private long GetCallerUserId() =>
        long.Parse(User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("Unauthorized access"));
}
