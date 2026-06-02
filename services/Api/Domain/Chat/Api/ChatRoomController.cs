using Api.Domain.Chat.Dto;
using Api.Domain.Chat.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Chat.Api;

[ApiController]
[Route("api/chat/rooms")]
[Authorize]
public class ChatRoomController : ControllerBase
{
    private readonly ChatRoomService _service;

    public ChatRoomController(ChatRoomService service)
    {
        _service = service;
    }

    [HttpPost("direct")]
    [SwaggerOperation(Summary = "Get or create a 1:1 chat room with a friend")]
    public async Task<ActionResult<ChatRoomGetResponseDto>> GetOrCreateDirect(
        [FromBody] ChatRoomDirectCreateRequestDto request)
    {
        var response = await _service.GetOrCreateDirectRoomAsync(request);
        return Ok(response);
    }

    [HttpPost("multi")]
    [SwaggerOperation(Summary = "Create a multi-user chat room")]
    public async Task<ActionResult<ChatRoomGetResponseDto>> CreateMulti(
        [FromBody] ChatRoomMultiCreateRequestDto request)
    {
        var response = await _service.CreateMultiRoomAsync(request);
        return Created($"/api/chat/rooms/{response.Id}", response);
    }

    [HttpGet]
    [SwaggerOperation(Summary = "List chat rooms the caller participates in")]
    public async Task<ActionResult<List<ChatRoomSummaryGetResponseDto>>> ListMine()
    {
        var response = await _service.GetMyRoomsAsync();
        if (response is null) return NoContent();
        return Ok(response);
    }

    [HttpGet("{roomId:long}")]
    [SwaggerOperation(Summary = "Get a chat room by id (participants only)")]
    public async Task<ActionResult<ChatRoomGetResponseDto>> GetById([FromRoute] long roomId)
    {
        var response = await _service.GetRoomByIdAsync(roomId);
        return Ok(response);
    }

    [HttpPost("{roomId:long}/participants")]
    [SwaggerOperation(Summary = "Add a participant (direct/multi: any participant; platform-group room: owner only)")]
    public async Task<ActionResult<ChatRoomParticipantGetResponseDto>> AddParticipant(
        [FromRoute] long roomId,
        [FromBody] ChatRoomParticipantAddRequestDto request)
    {
        var response = await _service.AddParticipantAsync(roomId, request);
        return Created($"/api/chat/rooms/{roomId}/participants/{response.Id}", response);
    }

    [HttpDelete("{roomId:long}/participants/me")]
    [SwaggerOperation(Summary = "Leave a chat room")]
    public async Task<IActionResult> Leave([FromRoute] long roomId)
    {
        await _service.LeaveRoomAsync(roomId);
        return NoContent();
    }
}
