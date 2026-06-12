using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Groups.Api
{
    [ApiController]
    [Route("api/groups")]
    [Authorize]
    public class GroupController(GroupService service, GroupJoinService joinService) : ControllerBase
    {
        private readonly GroupService _service = service;
        private readonly GroupJoinService _joinService = joinService;

        [HttpPost]
        [SwaggerOperation(Summary = "Create a group (caller becomes owner)")]
        public async Task<ActionResult<GroupResponseDto>> CreateGroup([FromBody] GroupCreateRequestDto request)
        {
            var response = await _service.CreateGroupAsync(request);
            return Created($"/api/groups/{response.Id}", response);
        }

        [HttpPost("{id:long}/join")]
        [SwaggerOperation(Summary = "Join a group immediately (open join policy only)")]
        public async Task<IActionResult> Join([FromRoute] long id)
        {
            await _joinService.JoinAsync(id);
            return Ok();
        }

        [HttpGet]
        [SwaggerOperation(Summary = "List public groups (discover)")]
        public async Task<ActionResult<List<GroupResponseDto>>> ListDiscoverable()
        {
            var response = await _service.ListDiscoverableGroupsAsync();
            if (response is null) return NoContent();
            return Ok(response);
        }

        [HttpGet("me")]
        [SwaggerOperation(Summary = "List groups the caller is a member of")]
        public async Task<ActionResult<List<GroupResponseDto>>> ListMine()
        {
            var response = await _service.ListMyGroupsAsync();
            if (response is null) return NoContent();
            return Ok(response);
        }

        [HttpGet("{id:long}")]
        [SwaggerOperation(Summary = "Get group info (private groups: members only)")]
        public async Task<ActionResult<GroupResponseDto>> GetGroup([FromRoute] long id)
        {
            var response = await _service.GetGroupAsync(id);
            return Ok(response);
        }

        [HttpPatch]
        [SwaggerOperation(Summary = "Update group settings (owner/admin)")]
        public async Task<ActionResult<GroupResponseDto>> UpdateGroup([FromBody] GroupPatchRequestDto request)
        {
            var response = await _service.UpdateGroupAsync(request);
            return Ok(response);
        }

        [HttpPatch("transfer")]
        [SwaggerOperation(Summary = "Transfer ownership to another member (owner only)")]
        public async Task<ActionResult<GroupResponseDto>> TransferOwnership([FromBody] GroupTransferOwnershipRequestDto request)
        {
            var response = await _service.TransferOwnershipAsync(request);
            return Ok(response);
        }

        [HttpDelete("{id:long}")]
        [SwaggerOperation(Summary = "Delete a group (owner only)")]
        public async Task<IActionResult> DeleteGroup([FromRoute] long id)
        {
            await _service.DeleteGroupAsync(id);
            return NoContent();
        }
    }
}
