using Api.Domain.Groups.Dto;
using Api.Domain.Groups.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Groups.Api
{
    [ApiController]
    [Authorize]
    public class GroupApplicationController(GroupApplicationService service) : ControllerBase
    {
        private readonly GroupApplicationService _service = service;

        [HttpPost("api/groups/{groupId:long}/applications")]
        [SwaggerOperation(Summary = "Apply to join a group")]
        public async Task<IActionResult> Apply([FromRoute] long groupId)
        {
            var result = await _service.ApplyAsync(groupId);
            return result.Outcome switch
            {
                GroupApplicationOutcome.GroupApplicationCreated => Created(
                    $"/api/applications/{result.Application!.Id}", result.Application),
                GroupApplicationOutcome.GroupMembershipCreatedFromReciprocalInvitation => Ok(),
                _ => throw new InvalidOperationException($"Unexpected apply outcome: {result.Outcome}"),
            };
        }

        [HttpGet("api/applications/me")]
        [SwaggerOperation(Summary = "List my pending applications and ignored outgoing applications")]
        public async Task<IActionResult> GetMyApplications()
        {
            var response = await _service.GetMyApplicationsAsync();
            if (response is null) return NoContent();
            return Ok(response);
        }

        [HttpGet("api/groups/{groupId:long}/applications")]
        [SwaggerOperation(Summary = "List pending applications (owner/admin)")]
        public async Task<ActionResult<List<GroupApplicationResponseDto>>> GetPending([FromRoute] long groupId)
        {
            var response = await _service.GetPendingByGroupAsync(groupId);
            return Ok(response);
        }

        [HttpGet("api/groups/{groupId:long}/applications/ignored")]
        [SwaggerOperation(Summary = "List ignored applications (owner/admin)")]
        public async Task<IActionResult> GetIgnored([FromRoute] long groupId)
        {
            var response = await _service.GetIgnoredByGroupAsync(groupId);
            if (response is null) return NoContent();
            return Ok(response);
        }

        [HttpPost("api/applications/{id:long}/ignore")]
        [SwaggerOperation(Summary = "Ignore an application (owner/admin)")]
        public async Task<IActionResult> Ignore([FromRoute] long id)
        {
            await _service.IgnoreAsync(id);
            return NoContent();
        }

        [HttpPost("api/applications/{id:long}/approve")]
        [SwaggerOperation(Summary = "Approve an application (owner/admin)")]
        public async Task<IActionResult> Approve([FromRoute] long id)
        {
            await _service.ApproveAsync(id);
            return Ok();
        }

        [HttpPost("api/applications/{id:long}/reject")]
        [SwaggerOperation(Summary = "Reject an application (owner/admin)")]
        public async Task<IActionResult> Reject([FromRoute] long id)
        {
            await _service.RejectAsync(id);
            return NoContent();
        }

        [HttpDelete("api/applications/{id:long}")]
        [SwaggerOperation(Summary = "Cancel an application (applicant only)")]
        public async Task<IActionResult> Cancel([FromRoute] long id)
        {
            await _service.CancelAsync(id);
            return NoContent();
        }
    }
}
