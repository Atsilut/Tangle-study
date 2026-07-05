using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Social.Dto;
using Social.Service;
using Swashbuckle.AspNetCore.Annotations;

namespace Social.Api;

[ApiController]
[Route("api/friendships")]
[Authorize]
public class FriendshipController(FriendshipService service) : ControllerBase
{
    private readonly FriendshipService _service = service;

    [HttpGet("me")]
    [SwaggerOperation(Summary = "List my accepted friends")]
    public async Task<ActionResult<List<FriendshipGetResponseDto>?>> GetMyFriends()
    {
        var response = await _service.GetMyFriendsAsync();
        if (response == null) return NoContent();
        return Ok(response);
    }

    [HttpGet("users/{userId:long}")]
    [SwaggerOperation(Summary = "List another user's accepted friends (subject to their privacy settings)")]
    public async Task<ActionResult<List<FriendshipGetResponseDto>?>> GetUserFriends([FromRoute] long userId)
    {
        var response = await _service.GetUserFriendsAsync(userId);
        if (response == null) return NoContent();
        return Ok(response);
    }

    [HttpDelete("{id:long}")]
    [SwaggerOperation(Summary = "Remove a friend")]
    public async Task<IActionResult> DeleteFriendship([FromRoute] long id)
    {
        await _service.DeleteFriendshipByIdAsync(id);
        return NoContent();
    }
}
