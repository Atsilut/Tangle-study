using Api.Domain.Chat.Dto;
using Api.Domain.Chat.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Chat.Api;

[ApiController]
[Route("api/chat/rooms/{roomId:long}/messages")]
[Authorize]
public class ChatMessageController : ControllerBase
{
    private readonly ChatMessageService _service;

    public ChatMessageController(ChatMessageService service)
    {
        _service = service;
    }

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

    [HttpPost]
    [SwaggerOperation(Summary = "Send a message to a chat room (participants only)")]
    public async Task<ActionResult<ChatMessageGetResponseDto>> Create(
        [FromRoute] long roomId,
        [FromBody] ChatMessageCreateRequestDto request)
    {
        var response = await _service.CreateMessageAsync(roomId, request);
        return Created($"/api/chat/rooms/{roomId}/messages/{response.Id}", response);
    }
}
