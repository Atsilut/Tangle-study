using System.Text.Json.Serialization;
using Location.Dto;
using Location.Config;
using Location.Infrastructure;
using Microsoft.Extensions.Options;

namespace Location.Service;

[Service]
public class GooglePlacesService(
    IHttpClientFactory httpClientFactory,
    IOptions<PlacesOptions> options,
    ILogger<GooglePlacesService> logger)
{
    private const string TextSearchUrl = "https://places.googleapis.com/v1/places:searchText";
    private const string GeocodeUrl = "https://maps.googleapis.com/maps/api/geocode/json";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly PlacesOptions _options = options.Value;
    private readonly ILogger<GooglePlacesService> _logger = logger;

    public bool IsConfigured => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<List<PlaceSearchResultDto>?> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return null;

        var client = _httpClientFactory.CreateClient("GooglePlaces");
        using var request = new HttpRequestMessage(HttpMethod.Post, TextSearchUrl)
        {
            Content = JsonContent.Create(new { textQuery = query, maxResultCount = limit }),
        };
        request.Headers.Add("X-Goog-Api-Key", _options.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", "places.id,places.displayName,places.location");

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Google Places text search failed ({StatusCode}): {Body}",
                (int)response.StatusCode,
                body);
            throw new InvalidOperationException("Place search is temporarily unavailable.");
        }

        var payload = await response.Content.ReadFromJsonAsync<GoogleTextSearchResponse>(cancellationToken);
        if (payload?.Places is null || payload.Places.Count == 0) return null;

        return [.. payload.Places
            .Where(place => place.Location is not null)
            .Select(place => new PlaceSearchResultDto(
                place.Id,
                place.DisplayName?.Text ?? query,
                (decimal)(place.Location!.Latitude ?? 0),
                (decimal)(place.Location.Longitude ?? 0)))];
    }

    public async Task<string?> ReverseGeocodeAsync(
        decimal latitude,
        decimal longitude,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured) return null;

        var client = _httpClientFactory.CreateClient("GooglePlaces");
        var url =
            $"{GeocodeUrl}?latlng={latitude},{longitude}&language=en&key={Uri.EscapeDataString(_options.ApiKey)}";

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google reverse geocode failed ({StatusCode})", (int)response.StatusCode);
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<GoogleGeocodeResponse>(cancellationToken);
        if (payload?.Status != "OK" || payload.Results is null || payload.Results.Count == 0) return null;

        return payload.Results[0].FormattedAddress?.Trim();
    }

    private sealed class GoogleTextSearchResponse
    {
        [JsonPropertyName("places")]
        public List<GooglePlace>? Places { get; init; }
    }

    private sealed class GooglePlace
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("displayName")]
        public GoogleDisplayName? DisplayName { get; init; }

        [JsonPropertyName("location")]
        public GoogleLocation? Location { get; init; }
    }

    private sealed class GoogleDisplayName
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }
    }

    private sealed class GoogleLocation
    {
        [JsonPropertyName("latitude")]
        public double? Latitude { get; init; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; init; }
    }

    private sealed class GoogleGeocodeResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("results")]
        public List<GoogleGeocodeResult>? Results { get; init; }
    }

    private sealed class GoogleGeocodeResult
    {
        [JsonPropertyName("formatted_address")]
        public string? FormattedAddress { get; init; }
    }
}
