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
        public async Task<ActionResult<FriendshipRequestResponseDto>> SendRequest([FromBody] FriendshipRequestCreateRequestDto request)
        {
            var response = await _service.SendRequestAsync(request);
            return Created($"/api/friendships/{response.Id}", response);
        }

        [HttpPost("{id:long}/accept")]
        [SwaggerOperation(Summary = "Accept a pending friend request")]
        public async Task<ActionResult<FriendshipRequestResponseDto>> Accept([FromRoute] long id)
        {
            var response = await _service.AcceptRequestAsync(id);
            return Ok(response);
        }

        [HttpPost("{id:long}/reject")]
        [SwaggerOperation(Summary = "Reject a pending friend request")]
        public async Task<ActionResult<FriendshipRequestResponseDto>> Reject([FromRoute] long id)
        {
            var response = await _service.RejectRequestAsync(id);
            return Ok(response);
        }

        [HttpGet("me")]
        [SwaggerOperation(Summary = "List my accepted friends")]
        public async Task<ActionResult<List<FriendshipRequestResponseDto>?>> GetMyFriends()
        {
            var response = await _service.GetMyFriendsAsync();
            if (response == null) return NoContent();
            return Ok(response);
        }

        [HttpGet("pending")]
        [SwaggerOperation(Summary = "List my pending friend requests (incoming and outgoing)")]
        public async Task<ActionResult<List<FriendshipRequestResponseDto>?>> GetPending()
        {
            var response = await _service.GetPendingAsync();
            if (response == null) return NoContent();
            return Ok(response);
        }

        [HttpDelete("{id:long}")]
        [SwaggerOperation(Summary = "Cancel a pending request, remove a friend, or clear a rejected row")]
        public async Task<IActionResult> DeleteFriendship([FromRoute] long id)
        {
            await _service.DeleteFriendshipByIdAsync(id);
            return NoContent();
        }
    }
}
