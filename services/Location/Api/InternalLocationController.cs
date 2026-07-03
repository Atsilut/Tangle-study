using Location.Dto;
using Location.Security;
using Location.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Location.Api;

[ApiController]
[Route("internal/location")]
public sealed class InternalLocationController(
    LocationClusterService clusterService,
    MapPinService mapPinService,
    LocationSessionService sessionService) : ControllerBase
{
    private readonly LocationClusterService _clusterService = clusterService;
    private readonly MapPinService _mapPinService = mapPinService;
    private readonly LocationSessionService _sessionService = sessionService;

    [ServiceFilter(typeof(WorkerCallbackAuthorizationFilter))]
    [HttpGet("cluster-points")]
    [SwaggerOperation(Summary = "Worker: list viewable pin coordinates in a bounding box for clustering")]
    public async Task<ActionResult<List<MapPinClusterPointDto>?>> GetClusterPoints([FromQuery] MapPinBoundsQueryDto query)
    {
        var result = await _clusterService.GetClusterPointsInBoundsAsync(query);
        if (result is null) return NoContent();
        return Ok(result);
    }

    [ServiceFilter(typeof(WorkerCallbackAuthorizationFilter))]
    [HttpPut("clusters")]
    [SwaggerOperation(Summary = "Worker callback: store clustered pins for a bounding box and zoom")]
    public async Task<IActionResult> StoreClusters([FromBody] LocationClusterStoreRequestDto request)
    {
        await _clusterService.StoreClustersAsync(request);
        return NoContent();
    }

    [ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
    [HttpPost("posts/{postId:long}/upsert")]
    public async Task<IActionResult> UpsertLocationForPost(
        [FromRoute] long postId,
        [FromBody] InternalLocationPostUpsertRequestDto request)
    {
        await _mapPinService.UpsertLocationForPostAsync(
            postId,
            request.UserId,
            request.Latitude,
            request.Longitude);
        return NoContent();
    }

    [ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
    [HttpPost("posts/{postId:long}/clear")]
    public async Task<IActionResult> ClearLocationForPost(
        [FromRoute] long postId,
        [FromBody] InternalLocationPostClearRequestDto request)
    {
        await _mapPinService.ClearLocationForPostAsync(postId, request.UserId);
        return NoContent();
    }

    [ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
    [HttpPost("posts/{postId:long}/clear-on-delete")]
    public async Task<IActionResult> ClearLocationForPostOnDelete([FromRoute] long postId)
    {
        await _mapPinService.ClearLocationForPostOnDeleteAsync(postId);
        return NoContent();
    }

    [ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
    [HttpPost("posts/locations-by-ids")]
    public async Task<ActionResult<InternalLocationPostLocationsResponseDto>> GetLocationsByPostIds(
        [FromBody] InternalLocationPostIdsRequestDto request)
    {
        var locations = await _mapPinService.GetLocationsByPostIdsAsync(request.PostIds);
        return Ok(new InternalLocationPostLocationsResponseDto(
            [.. locations.Select(entry => new InternalLocationPostLocationEntryDto(
                entry.Key,
                entry.Value.Latitude,
                entry.Value.Longitude))]));
    }

    [ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
    [HttpPost("users/{userId:long}/detach-on-deletion")]
    public async Task<IActionResult> DetachUserOnDeletion([FromRoute] long userId)
    {
        await _mapPinService.HandleUserDeletionAsync(userId);
        await _sessionService.HandleUserDeletionAsync(userId);
        return NoContent();
    }

    [ServiceFilter(typeof(InternalAccessAuthorizationFilter))]
    [HttpPost("groups/{groupId:long}/end-sessions")]
    public async Task<IActionResult> EndSessionsForGroup([FromRoute] long groupId)
    {
        await _sessionService.EndSessionsForGroupAsync(groupId);
        return NoContent();
    }
}

public sealed record InternalLocationPostUpsertRequestDto(long UserId, decimal Latitude, decimal Longitude);

public sealed record InternalLocationPostClearRequestDto(long UserId);

public sealed record InternalLocationPostIdsRequestDto(long[] PostIds);

public sealed record InternalLocationPostLocationEntryDto(long PostId, decimal Latitude, decimal Longitude);

public sealed record InternalLocationPostLocationsResponseDto(IReadOnlyList<InternalLocationPostLocationEntryDto> Locations);
