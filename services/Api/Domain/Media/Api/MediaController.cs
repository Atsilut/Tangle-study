using Api.Domain.Media.Dto;
using Api.Domain.Media.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Media.Api;

[ApiController]
[Route("api/media")]
[Authorize]
public sealed class MediaController(MediaService service) : ControllerBase
{
    private readonly MediaService _service = service;

    [HttpGet("{id:long}")]
    [SwaggerOperation(Summary = "Get media asset metadata and processing status")]
    public async Task<ActionResult<MediaAssetGetResponseDto>> GetById([FromRoute] long id) =>
        Ok(await _service.GetMediaAssetByIdAsync(id));

    [HttpPost("upload-init")]
    [SwaggerOperation(Summary = "Initialize a direct-to-storage media upload")]
    public async Task<ActionResult<MediaUploadInitResponseDto>> InitUpload([FromBody] MediaUploadInitRequestDto request) =>
        Ok(await _service.InitUploadAsync(request));

    [HttpPost("{id:long}/complete")]
    [SwaggerOperation(Summary = "Confirm a direct-to-storage upload and enqueue processing")]
    public async Task<ActionResult<MediaAssetGetResponseDto>> CompleteUpload([FromRoute] long id) =>
        Ok(await _service.CompleteUploadAsync(id));

    [HttpDelete("{id:long}")]
    [SwaggerOperation(Summary = "Delete an unlinked media asset owned by the current user")]
    public async Task<IActionResult> DeleteUnlinked([FromRoute] long id)
    {
        await _service.DeleteUnlinkedMediaAssetByIdAsync(id);
        return NoContent();
    }
}
