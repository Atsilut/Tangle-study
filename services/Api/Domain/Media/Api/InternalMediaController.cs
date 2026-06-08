using Api.Domain.Media.Dto;
using Api.Domain.Media.Service;
using Api.Global.Security;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Media.Api;

[ApiController]
[Route("internal/media")]
[ServiceFilter(typeof(WorkerCallbackAuthorizationFilter))]
public sealed class InternalMediaController(MediaService service) : ControllerBase
{
    private readonly MediaService _service = service;

    [HttpPatch("{id:long}/processed")]
    [SwaggerOperation(Summary = "Worker callback when media processing finishes")]
    public async Task<IActionResult> ReportProcessed(
        [FromRoute] long id,
        [FromBody] MediaProcessedRequestDto request)
    {
        await _service.ReportProcessedAsync(id, request);
        return NoContent();
    }
}
