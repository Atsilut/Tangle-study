using Api.Domain.Location.Dto;
using Api.Domain.Location.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Location.Api;

[ApiController]
[Route("api/location/pins")]
public class MapPinController(MapPinService service) : ControllerBase
{
    private readonly MapPinService _service = service;

    [HttpPost]
    [Authorize]
    [SwaggerOperation(Summary = "Create map pin")]
    public async Task<ActionResult<MapPinGetResponseDto>> CreateMapPin([FromBody] MapPinCreateRequestDto request)
    {
        var response = await _service.CreateMapPinAsync(request);
        return Created($"/api/location/pins/{response.Id}", response);
    }

    [HttpGet]
    [SwaggerOperation(Summary = "Get map pins in bounding box")]
    public async Task<ActionResult<List<MapPinGetResponseDto>?>> GetMapPinsInBounds([FromQuery] MapPinBoundsQueryDto query)
    {
        var result = await _service.GetMapPinsInBoundsAsync(query);
        if (result is null) return NoContent();
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [SwaggerOperation(Summary = "Get map pin by id")]
    public async Task<ActionResult<MapPinGetResponseDto?>> GetMapPinById([FromRoute] long id)
    {
        var result = await _service.GetMapPinByIdAsync(id);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    [Authorize]
    [SwaggerOperation(Summary = "Delete map pin")]
    public async Task<IActionResult> DeleteMapPin([FromRoute] long id)
    {
        await _service.DeleteMapPinByIdAsync(id);
        return NoContent();
    }
}
