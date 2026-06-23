using Api.Domain.Location.Dto;
using Api.Domain.Location.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Location.Api;

[ApiController]
[Route("api/location/sessions")]
[Authorize]
public class LocationSessionController(
    LocationSessionService service,
    LocationSafetyAlertService safetyAlertService) : ControllerBase
{
    private readonly LocationSessionService _service = service;
    private readonly LocationSafetyAlertService _safetyAlertService = safetyAlertService;

    [HttpPost]
    [SwaggerOperation(Summary = "Start live location sharing with a group")]
    public async Task<ActionResult<LocationSessionGetResponseDto>> StartSession(
        [FromBody] LocationSessionCreateRequestDto request)
    {
        var response = await _service.StartSessionAsync(request);
        return Created($"/api/location/sessions/{response.Id}", response);
    }

    [HttpGet("mine")]
    [SwaggerOperation(Summary = "Get the caller's active location session in a group")]
    public async Task<ActionResult<LocationSessionGetResponseDto?>> GetMyActiveSession([FromQuery] long groupId)
    {
        var result = await _service.GetMyActiveSessionAsync(groupId);
        if (result is null) return NoContent();
        return Ok(result);
    }

    [HttpGet("active")]
    [SwaggerOperation(Summary = "Get active live locations for group members")]
    public async Task<ActionResult<List<LiveLocationGetResponseDto>?>> GetActiveGroupLocations([FromQuery] long groupId)
    {
        var result = await _service.GetActiveGroupLocationsAsync(groupId);
        if (result is null) return NoContent();
        return Ok(result);
    }

    [HttpGet("members")]
    [SwaggerOperation(Summary = "Get sharing status for other members in a group")]
    public async Task<ActionResult<List<GroupMemberLocationStatusDto>>> GetGroupMemberSharingStatus(
        [FromQuery] long groupId) =>
        Ok(await _service.GetGroupMemberSharingStatusAsync(groupId));

    [HttpPatch("{id:long}/position")]
    [SwaggerOperation(Summary = "Update live location position")]
    public async Task<ActionResult<LocationSessionGetResponseDto>> UpdatePosition(
        [FromRoute] long id,
        [FromBody] LocationPositionUpdateRequestDto request) =>
        Ok(await _service.UpdatePositionAsync(id, request));

    [HttpDelete("{id:long}")]
    [SwaggerOperation(Summary = "Stop live location sharing")]
    public async Task<IActionResult> StopSession([FromRoute] long id)
    {
        await _service.StopSessionAsync(id);
        return NoContent();
    }

    [HttpPost("{id:long}/sos")]
    [SwaggerOperation(Summary = "Send an SOS safety alert to group members")]
    public async Task<ActionResult<LocationSafetyAlertDto>> TriggerSos([FromRoute] long id) =>
        Ok(await _safetyAlertService.TriggerSosAsync(id));
}
