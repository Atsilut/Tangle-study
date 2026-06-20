using Api.Domain.Location.Dto;
using Api.Domain.Location.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Location.Api;

[ApiController]
[Route("api/location/sessions")]
[Authorize]
public class LocationSessionController(LocationSessionService service) : ControllerBase
{
    private readonly LocationSessionService _service = service;

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
}
