using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Groups.Api
{
    [ApiController]
    [Route("api/groups/{groupId:long}/members")]
    [Authorize]
    public class GroupMemberController(GroupMembershipService service) : ControllerBase
    {
        private readonly GroupMembershipService _service = service;

        [HttpGet]
        [SwaggerOperation(Summary = "List members of a group")]
        public async Task<ActionResult<List<GroupMemberGetResponseDto>?>> GetMembers([FromRoute] long groupId)
        {
            var response = await _service.GetMembersAsync(groupId);
            if (response == null) return NoContent();
            return Ok(response);
        }

        [HttpPatch("{userId:long}")]
        [SwaggerOperation(Summary = "Promote or demote a member (owner only)")]
        public async Task<ActionResult<GroupMemberGetResponseDto>> UpdateRole(
            [FromRoute] long groupId,
            [FromRoute] long userId,
            [FromBody] GroupMemberRolePatchRequestDto request)
        {
            var response = await _service.UpdateRoleAsync(groupId, userId, request);
            return Ok(response);
        }

        [HttpDelete("{userId:long}")]
        [SwaggerOperation(Summary = "Leave (self), kick a member (admin/owner), or remove an admin (owner only)")]
        public async Task<IActionResult> RemoveMember([FromRoute] long groupId, [FromRoute] long userId)
        {
            await _service.RemoveMemberAsync(groupId, userId);
            return NoContent();
        }
    }
}
