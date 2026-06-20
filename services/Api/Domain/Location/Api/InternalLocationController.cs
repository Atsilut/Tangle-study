using Api.Domain.Location.Dto;
using Api.Domain.Location.Service;
using Api.Global.Security;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Location.Api;

[ApiController]
[Route("internal/location")]
[ServiceFilter(typeof(WorkerCallbackAuthorizationFilter))]
public sealed class InternalLocationController(LocationClusterService service) : ControllerBase
{
    private readonly LocationClusterService _service = service;

    [HttpGet("cluster-points")]
    [SwaggerOperation(Summary = "Worker: list viewable pin coordinates in a bounding box for clustering")]
    public async Task<ActionResult<List<MapPinClusterPointDto>?>> GetClusterPoints([FromQuery] MapPinBoundsQueryDto query)
    {
        var result = await _service.GetClusterPointsInBoundsAsync(query);
        if (result is null) return NoContent();
        return Ok(result);
    }

    [HttpPut("clusters")]
    [SwaggerOperation(Summary = "Worker callback: store clustered pins for a bounding box and zoom")]
    public async Task<IActionResult> StoreClusters([FromBody] LocationClusterStoreRequestDto request)
    {
        await _service.StoreClustersAsync(request);
        return NoContent();
    }
}
