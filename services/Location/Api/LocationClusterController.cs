using Location.Dto;
using Location.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace Location.Api;

[ApiController]
[Authorize]
[Route("api/location/clusters")]
[EnableRateLimiting("location-clusters")]
public class LocationClusterController(LocationClusterService service) : ControllerBase
{
    private readonly LocationClusterService _service = service;

    [HttpGet]
    [SwaggerOperation(Summary = "Get clustered map pins for a bounding box and zoom level")]
    public async Task<ActionResult<List<MapClusterGetResponseDto>?>> GetClusters([FromQuery] MapClusterBoundsQueryDto query)
    {
        var result = await _service.GetClustersAsync(query);
        if (result is null) return NoContent();
        return Ok(result);
    }
}
