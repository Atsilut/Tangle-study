using Tangle.AspNetCore.Security;
using Chat.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Chat.Api;

[ApiController]
[Route("internal/chat")]
[ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
public sealed class InternalChatController(
    ChatMessageService chatMessageService,
    ChatRoomService chatRoomService) : ControllerBase
{
    [HttpPost("users/{userId:long}/detach-on-deletion")]
    [SwaggerOperation(Summary = "Detach a deleted user from chat rooms and messages (users-service user deletion)")]
    public async Task<IActionResult> DetachUserOnDeletion([FromRoute] long userId)
    {
        await chatMessageService.DetachSenderFromDeletedUserAsync(userId);
        await chatRoomService.DetachUserFromDeletedUserAsync(userId);
        return NoContent();
    }

    [Authorize]
    [HttpPost("messages/{chatMessageId:long}/media-view")]
    [SwaggerOperation(Summary = "Verify caller may view media attached to a chat message")]
    public async Task<IActionResult> EnsureCanViewChatMessageMedia([FromRoute] long chatMessageId)
    {
        await chatMessageService.EnsureCurrentUserCanAccessMessageAsync(chatMessageId);
        return NoContent();
    }
}
