using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Group.Config;
using Group.Exceptions;
using Microsoft.Extensions.Options;

namespace Group.Client;

internal sealed class HttpSocialClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<SocialClientOptions> options) : ISocialClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly SocialClientOptions _options = options.Value;

    public async Task<bool> IsBlockedByAsync(
        long blockerUserId,
        long blockedUserId,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            "internal/social/blocks/is-blocked-by",
            new SocialIsBlockedRequestDto(blockerUserId, blockedUserId),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<SocialIsBlockedResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Social is-blocked-by response was empty.");

        return payload.IsBlocked;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        object? content,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpSocialClient));
        using var request = new HttpRequestMessage(method, relativePath);

        if (content is not null)
            request.Content = JsonContent.Create(content, options: SerializerOptions);

        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation("X-Internal-Secret", _options.InternalSecret);

        return await client.SendAsync(request, cancellationToken);
    }

    private async Task ThrowForFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await ReadProblemDetailAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(detail);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new EntityNotFoundException(detail);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var isAuthenticated = _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
            if (isAuthenticated)
                throw new AccessForbiddenException(detail);
            throw new UnauthorizedAccessException(detail);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new AccessForbiddenException(detail);

        throw new InvalidOperationException(
            $"Social access check failed ({(int)response.StatusCode}): {detail}");
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
}
