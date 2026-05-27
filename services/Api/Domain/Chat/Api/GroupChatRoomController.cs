using Api.Domain.Chat.Dto;
using Api.Domain.Chat.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Chat.Api;

[ApiController]
[Route("api/groups/{groupId:long}/chat-rooms")]
[Authorize]
public class GroupChatRoomController : ControllerBase
{
    private readonly ChatRoomService _service;

    public GroupChatRoomController(ChatRoomService service)
    {
        _service = service;
    }

    [HttpGet]
    [SwaggerOperation(Summary = "List chat rooms under a platform group (members only)")]
    public async Task<ActionResult<List<ChatRoomSummaryGetResponseDto>>> List([FromRoute] long groupId)
    {
        var response = await _service.GetPlatformGroupRoomsAsync(groupId);
        if (response is null) return NoContent();
        return Ok(response);
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Create a chat room under a platform group")]
    public async Task<ActionResult<ChatRoomGetResponseDto>> Create(
        [FromRoute] long groupId,
        [FromBody] ChatRoomPlatformGroupCreateRequestDto request)
    {
        var response = await _service.CreatePlatformGroupRoomAsync(groupId, request);
        return Created($"/api/chat/rooms/{response.Id}", response);
    }
}
