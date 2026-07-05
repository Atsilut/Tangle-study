using Users.Service;
using Users.Dto;
using Users.Security;
using Microsoft.AspNetCore.Mvc;

namespace Users.Api;

[ApiController]
[Route("internal/users")]
[ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
public sealed class InternalUsersController(UserService userService) : ControllerBase
{
    [HttpPost("{userId:long}/exists")]
    public async Task<IActionResult> EnsureUserExists([FromRoute] long userId)
    {
        await userService.EnsureUserExistsAsync(userId, "Authentication failed", StatusCodes.Status400BadRequest);
        return NoContent();
    }

    [HttpPost("{userId:long}/validate")]
    public async Task<IActionResult> ValidateUserExists([FromRoute] long userId)
    {
        await userService.EnsureUserExistsAsync(userId, "User not found", StatusCodes.Status400BadRequest);
        return NoContent();
    }

    [HttpPost("validate-exist")]
    public async Task<IActionResult> EnsureUsersExist([FromBody] InternalUsersUserIdsRequestDto request)
    {
        await userService.EnsureUsersExistAsync(
            request.UserIds,
            "User not found",
            StatusCodes.Status400BadRequest);
        return NoContent();
    }

    [HttpPost("nicknames")]
    public async Task<ActionResult<InternalUsersNicknamesResponseDto>> GetNicknames(
        [FromBody] InternalUsersUserIdsRequestDto request)
    {
        var nicknames = await userService.GetNicknamesByUserIdsAsync(request.UserIds);
        return Ok(new InternalUsersNicknamesResponseDto(
            [.. request.UserIds
                .Distinct()
                .Select(id => new InternalUsersNicknameEntryDto(id, nicknames.GetValueOrDefault(id, "Deleted User")))]));
    }

    [HttpPost("by-nickname")]
    public async Task<ActionResult<InternalUsersNicknameLookupResponseDto>> GetUserIdByNickname(
        [FromBody] InternalUsersNicknameLookupRequestDto request)
    {
        var user = await userService.GetUserByNicknameAsync(request.Nickname);
        if (user is null) return NotFound();
        return Ok(new InternalUsersNicknameLookupResponseDto(user.Id));
    }

    [HttpPost("{userId:long}/friends-list-visibility")]
    public async Task<ActionResult<InternalUsersFriendsListVisibilityResponseDto>> GetFriendsListVisibility(
        [FromRoute] long userId)
    {
        var visibility = await userService.GetFriendsListVisibilityAsync(userId);
        return Ok(new InternalUsersFriendsListVisibilityResponseDto(visibility));
    }
}
