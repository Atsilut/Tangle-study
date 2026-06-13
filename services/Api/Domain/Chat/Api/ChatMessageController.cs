using Api.Domain.Chat.Dto;
using Api.Domain.Chat.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Chat.Api;

[ApiController]
[Route("api/chat/rooms/{roomId:long}/messages")]
[Authorize]
public class ChatMessageController(ChatMessageService service) : ControllerBase
{
    private readonly ChatMessageService _service = service;

    [HttpGet]
    [SwaggerOperation(Summary = "List messages in a chat room (participants only, cursor pagination)")]
    public async Task<ActionResult<List<ChatMessageGetResponseDto>>> List(
        [FromRoute] long roomId,
        [FromQuery] long? before,
        [FromQuery] int? limit)
    {
        var response = await _service.GetMessagesForRoomAsync(roomId, before, limit);
        if (response is null) return NoContent();
        return Ok(response);
    }

    [HttpPost("seen")]
    [SwaggerOperation(Summary = "Mark messages as seen by the current user (participants only)")]
    public async Task<IActionResult> MarkSeen(
        [FromRoute] long roomId,
        [FromBody] ChatMessageMarkSeenRequestDto request)
    {
        await _service.MarkMessagesSeenAsync(roomId, request);
        return NoContent();
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Send a message to a chat room (participants only)")]
    public async Task<ActionResult<ChatMessageGetResponseDto>> Create(
        [FromRoute] long roomId,
        [FromBody] ChatMessageCreateRequestDto request)
    {
        var response = await _service.CreateMessageAsync(roomId, request);
        return Created($"/api/chat/rooms/{roomId}/messages/{response.Id}", response);
    }

    [HttpPatch("{messageId:long}")]
    [SwaggerOperation(Summary = "Edit a chat message (sender only, within policy window)")]
    public async Task<ActionResult<ChatMessageGetResponseDto>> Update(
        [FromRoute] long roomId,
        [FromRoute] long messageId,
        [FromBody] ChatMessagePatchRequestDto request)
    {
        var response = await _service.UpdateMessageAsync(roomId, messageId, request);
        return Ok(response);
    }

    [HttpDelete("{messageId:long}")]
    [SwaggerOperation(Summary = "Soft-delete a chat message (sender only, within policy window, unseen by others)")]
    public async Task<IActionResult> Delete([FromRoute] long roomId, [FromRoute] long messageId)
    {
        await _service.DeleteMessageAsync(roomId, messageId);
        return NoContent();
    }
}
