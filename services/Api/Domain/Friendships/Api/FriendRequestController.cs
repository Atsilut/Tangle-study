using Api.Domain.Friendships.Dto;
using Api.Domain.Friendships.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Friendships.Api
{
    [ApiController]
    [Route("api/friendships/requests")]
    [Authorize]
    public class FriendRequestController : ControllerBase
    {
        private readonly FriendRequestService _service;

        public FriendRequestController(FriendRequestService service)
        {
            _service = service;
        }

        [HttpPost]
        [SwaggerOperation(Summary = "Send a friend request")]
        public async Task<IActionResult> SendRequest([FromBody] FriendRequestCreateRequestDto request)
        {
            var outcome = await _service.SendRequestAsync(request);
            return outcome switch
            {
                SendFriendRequestOutcome.FriendRequestCreated => Created(),
                SendFriendRequestOutcome.FriendshipCreatedFromReciprocalRequest => Ok(),
                _ => throw new InvalidOperationException($"Unexpected send friend request outcome: {outcome}"),
            };
        }

        [HttpPost("{id:long}/accept")]
        [SwaggerOperation(Summary = "Accept a pending friend request")]
        public async Task<ActionResult<FriendshipResponseDto>> Accept([FromRoute] long id)
        {
            await _service.AcceptRequestAsync(id);
            return Ok();
        }

        [HttpGet("pending")]
        [SwaggerOperation(Summary = "List my pending friend requests (incoming and outgoing)")]
        public async Task<ActionResult<List<FriendRequestResponseDto>?>> GetPending()
        {
            var response = await _service.GetPendingAsync();
            if (response == null) return NoContent();
            return Ok(response);
        }

        [HttpDelete("{id:long}")]
        [SwaggerOperation(Summary = "Cancel a pending friend request")]
        public async Task<IActionResult> DeleteRequest([FromRoute] long id)
        {
            await _service.DeleteRequestByIdAsync(id);
            return NoContent();
        }

        [HttpDelete("{id:long}/reject")]
        [SwaggerOperation(Summary = "Reject a pending friend request (addressee only; removes the request)")]
        public async Task<IActionResult> Reject([FromRoute] long id)
        {
            await _service.RejectRequestAsync(id);
            return NoContent();
        }
    }
}
