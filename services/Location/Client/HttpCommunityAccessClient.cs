using System.Net.Http.Json;
using System.Text.Json;
using Location.Config;
using Location.Exceptions;
using Microsoft.Extensions.Options;

namespace Location.Client;

internal sealed class HttpCommunityAccessClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<CommunityClientOptions> options) : ICommunityAccessClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly CommunityClientOptions _options = options.Value;

    public Task EnsurePostOwnerAsync(long postId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/community/{postId}/validate-owner", cancellationToken);

    public async Task<HashSet<long>> GetViewablePostIdsAsync(
        IReadOnlyCollection<long> postIds,
        long? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var ids = postIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        var response = await PostAsync(
            "internal/community/viewable-ids",
            new InternalCommunityViewableIdsRequestDto(ids, viewerUserId),
            cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<InternalCommunityViewableIdsResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Community viewable-ids response was empty.");

        return [.. payload.ViewablePostIds];
    }

    private async Task PostNoContentAsync(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await PostAsync(relativePath, content: null, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        await ThrowForFailureAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> PostAsync(
        string relativePath,
        object? content,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpCommunityAccessClient));
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);

        if (content is not null)
            request.Content = JsonContent.Create(content, options: SerializerOptions);

        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation("X-Internal-Secret", _options.InternalSecret);

        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task ThrowForFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await ReadProblemDetailAsync(response, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            throw new ArgumentException(detail);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new EntityNotFoundException(detail);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException(detail);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new AccessForbiddenException(detail);

        throw new InvalidOperationException(
            $"Community access check failed ({(int)response.StatusCode}): {detail}");
    }

    private static async Task<string> ReadProblemDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
            return response.ReasonPhrase ?? "Access denied";

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detailElement)
                && detailElement.ValueKind == JsonValueKind.String)
                return detailElement.GetString() ?? body;
        }
        catch (JsonException)
        {
        }

        return body;
    }

    private sealed record InternalCommunityViewableIdsRequestDto(long[] PostIds, long? ViewerUserId);

    private sealed record InternalCommunityViewableIdsResponseDto(long[] ViewablePostIds);
}
