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

        [HttpPost]
        [SwaggerOperation(Summary = "Send a friend request")]
        public async Task<ActionResult<FriendshipResponseDto>> SendRequest([FromBody] FriendshipCreateRequestDto request)
        {
            var response = await _service.SendRequestAsync(request);
            return Created($"/api/friendships/{response.Id}", response);
        }

        [HttpPost("{id:long}/accept")]
        [SwaggerOperation(Summary = "Accept a pending friend request")]
        public async Task<ActionResult<FriendshipResponseDto>> Accept([FromRoute] long id)
        {
            var response = await _service.AcceptRequestAsync(id);
            return Ok(response);
        }

        [HttpPost("{id:long}/reject")]
        [SwaggerOperation(Summary = "Reject a pending friend request")]
        public async Task<ActionResult<FriendshipResponseDto>> Reject([FromRoute] long id)
        {
            var response = await _service.RejectRequestAsync(id);
            return Ok(response);
        }

        [HttpDelete("{id:long}")]
        [SwaggerOperation(Summary = "Cancel a pending request, remove a friend, or clear a rejected row")]
        public async Task<IActionResult> Remove([FromRoute] long id)
        {
            await _service.RemoveAsync(id);
            return NoContent();
        }

        [HttpGet("me")]
        [SwaggerOperation(Summary = "List my accepted friends")]
        public async Task<ActionResult<List<FriendshipResponseDto>>> GetMyFriends()
        {
            var response = await _service.GetMyFriendsAsync();
            return Ok(response);
        }

        [HttpGet("pending")]
        [SwaggerOperation(Summary = "List my pending friend requests (incoming and outgoing)")]
        public async Task<ActionResult<List<FriendshipResponseDto>>> GetPending()
        {
            var response = await _service.GetPendingAsync();
            return Ok(response);
        }
    }
}
