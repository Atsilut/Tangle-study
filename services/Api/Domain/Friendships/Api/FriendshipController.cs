using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Friendships.Api
{
    [ApiController]
    [Route("api/friendships")]
    [Authorize]
    public class FriendshipController : ControllerBase
    {
        private readonly FriendshipService _service;

        public FriendshipController(FriendshipService service)
        {
            _service = service;
        }

        [HttpGet("me")]
        [SwaggerOperation(Summary = "List my accepted friends")]
        public async Task<ActionResult<List<FriendshipResponseDto>?>> GetMyFriends()
        {
            var response = await _service.GetMyFriendsAsync();
            if (response == null) return NoContent();
            return Ok(response);
        }

        [HttpGet("users/{userId:long}")]
        [SwaggerOperation(Summary = "List another user's accepted friends (subject to their privacy settings)")]
        public async Task<ActionResult<List<FriendshipResponseDto>?>> GetUserFriends([FromRoute] long userId)
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
}
