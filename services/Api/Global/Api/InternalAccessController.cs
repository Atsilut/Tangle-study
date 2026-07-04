using Api.Domain.Users.Service;
using Api.Global.Dto;
using Api.Global.Security;
using Microsoft.AspNetCore.Mvc;

namespace Api.Global.Api;

[ApiController]
[Route("internal/access")]
[ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
public sealed class InternalAccessController(UserService userService) : ControllerBase
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

    [HttpPost("users/{userId:long}/friends-list-visibility")]
    public async Task<ActionResult<InternalAccessFriendsListVisibilityResponseDto>> GetFriendsListVisibility(
        [FromRoute] long userId)
    {
        var visibility = await userService.GetFriendsListVisibilityAsync(userId);
        return Ok(new InternalAccessFriendsListVisibilityResponseDto(visibility));
    }
}
