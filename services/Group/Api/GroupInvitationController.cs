using Group.Dto;
using Group.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Group.Api
{
    [ApiController]
    [Authorize]
    public class GroupInvitationController(GroupInvitationService service) : ControllerBase
    {
        private readonly GroupInvitationService _service = service;

        [HttpGet("api/groups/{groupId:long}/invitations")]
        [SwaggerOperation(Summary = "List pending invitations for a group (members per invite policy)")]
        public async Task<IActionResult> ListForGroup([FromRoute] long groupId)
        {
            var response = await _service.GetPendingForGroupAsync(groupId);
            if (response is null) return NoContent();
            return Ok(response);
        }

        [HttpPost("api/groups/{groupId:long}/invitations")]
        [SwaggerOperation(Summary = "Invite a user to a group (per group invite policy)")]
        public async Task<IActionResult> Invite(
            [FromRoute] long groupId,
            [FromBody] GroupInvitationCreateRequestDto request)
        {
            var result = await _service.InviteAsync(groupId, request);
            return result.Outcome switch
            {
                GroupInvitationOutcome.GroupInvitationCreated => result.Invitation is { } invitation
                    ? Created($"/api/invitations/{invitation.Id}", invitation)
                    : throw new InvalidOperationException($"Invitation missing for outcome {result.Outcome}"),
                GroupInvitationOutcome.GroupMembershipCreatedFromReciprocalApplication => Ok(),
                _ => throw new InvalidOperationException($"Unexpected invite outcome: {result.Outcome}"),
            };
        }

        [HttpPost("api/invitations/{id:long}/ignore")]
        [SwaggerOperation(Summary = "Ignore an invitation (invitee)")]
        public async Task<IActionResult> Ignore([FromRoute] long id)
        {
            await _service.IgnoreAsync(id);
            return NoContent();
        }

        [HttpPost("api/invitations/{id:long}/accept")]
        [SwaggerOperation(Summary = "Accept an invitation (invitee)")]
        public async Task<IActionResult> Accept([FromRoute] long id)
        {
            await _service.AcceptAsync(id);
            return Ok();
        }

        [HttpPost("api/invitations/{id:long}/reject")]
        [SwaggerOperation(Summary = "Reject an invitation (invitee)")]
        public async Task<IActionResult> Reject([FromRoute] long id)
        {
            await _service.RejectAsync(id);
            return NoContent();
        }

        [HttpDelete("api/invitations/{id:long}")]
        [SwaggerOperation(Summary = "Cancel an invitation (inviter or admin/owner)")]
        public async Task<IActionResult> Cancel([FromRoute] long id)
        {
            await _service.CancelAsync(id);
            return NoContent();
        }

        [HttpGet("api/invitations/me")]
        [SwaggerOperation(Summary = "List my pending invitations and ignored outgoing invitations")]
        public async Task<IActionResult> GetMyPending()
        {
            var response = await _service.GetMyPendingAsync();
            if (response is null) return NoContent();
            return Ok(response);
        }

        [HttpGet("api/invitations/ignored")]
        [SwaggerOperation(Summary = "List invitations I ignored (incoming)")]
        public async Task<IActionResult> GetIgnoredIncoming()
        {
            var response = await _service.GetIgnoredIncomingAsync();
            if (response is null) return NoContent();
            return Ok(response);
        }
    }
}
