using Api.Domain.Location.Dto;
using Api.Domain.Location.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace Api.Domain.Location.Api;

[ApiController]
[Route("api/location/places")]
[EnableRateLimiting("places")]
public class PlaceController(GooglePlacesService service) : ControllerBase
{
    private readonly GooglePlacesService _service = service;

    [HttpGet("search")]
    [SwaggerOperation(Summary = "Search places via Google Places")]
    public async Task<ActionResult<List<PlaceSearchResultDto>?>> SearchPlaces(
        [FromQuery] string q,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var trimmed = q.Trim();
        if (trimmed.Length < 2) return NoContent();

        var results = await _service.SearchAsync(trimmed, Math.Clamp(limit, 1, 10), cancellationToken);
        if (results is null || results.Count == 0) return NoContent();
        return Ok(results);
    }

    [HttpGet("reverse")]
    [SwaggerOperation(Summary = "Reverse geocode coordinates via Google Geocoding")]
    public async Task<ActionResult<PlaceReverseResponseDto?>> ReverseGeocode(
        [FromQuery] decimal latitude,
        [FromQuery] decimal longitude,
        CancellationToken cancellationToken = default)
    {
        LocationCoordinateValidation.Validate(latitude, longitude);

        var displayName = await _service.ReverseGeocodeAsync(latitude, longitude, cancellationToken);
        if (displayName is null) return NoContent();
        return Ok(new PlaceReverseResponseDto(displayName));
    }
}
