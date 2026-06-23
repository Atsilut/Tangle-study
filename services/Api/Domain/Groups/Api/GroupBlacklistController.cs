using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Groups.Api
{
    [ApiController]
    [Route("api/groups/{groupId:long}/blacklist")]
    [Authorize]
    public class GroupBlacklistController(GroupBlacklistService service) : ControllerBase
    {
        private readonly GroupBlacklistService _service = service;

        [HttpPost]
        [SwaggerOperation(Summary = "Blacklist a user from a group (owner only); kicks member and clears pending join requests")]
        public async Task<ActionResult<GroupBlacklistGetResponseDto>> Add(
            [FromRoute] long groupId,
            [FromBody] GroupBlacklistCreateRequestDto request)
        {
            var response = await _service.AddAsync(groupId, request);
            return Created($"/api/groups/{groupId}/blacklist/{response.UserId}", response);
        }

        [HttpGet]
        [SwaggerOperation(Summary = "List blacklisted users for a group (owner only)")]
        public async Task<ActionResult<List<GroupBlacklistGetResponseDto>>> List([FromRoute] long groupId)
        {
            var response = await _service.ListAsync(groupId);
            return Ok(response);
        }

        [HttpDelete("{userId:long}")]
        [SwaggerOperation(Summary = "Remove a user from the group blacklist (owner only)")]
        public async Task<IActionResult> Remove([FromRoute] long groupId, [FromRoute] long userId)
        {
            await _service.RemoveAsync(groupId, userId);
            return NoContent();
        }
    }
}
