using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Community.Config;
using Community.Exceptions;

namespace Community.Client;

internal sealed class HttpMonolithAccessClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<MonolithOptions> options) : IMonolithAccessClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly MonolithOptions _options = options.Value;

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/access/users/{userId}/validate", cancellationToken);

    public async Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<long, string>();

        var response = await PostAsync(
            "internal/access/users/nicknames",
            new InternalAccessUserIdsRequestDto(ids),
            cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<InternalAccessNicknamesResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Monolith nicknames response was empty.");

        return payload.Nicknames.ToDictionary(entry => entry.UserId, entry => entry.Nickname);
    }

    public async Task<long?> GetUserIdByNicknameAsync(string nickname, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "internal/access/users/by-nickname",
            new InternalAccessNicknameLookupRequestDto(nickname),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) await ThrowForFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<InternalAccessNicknameLookupResponseDto>(
            SerializerOptions,
            cancellationToken);
        return payload?.UserId;
    }

    private Task PostNoContentAsync(string relativePath, CancellationToken cancellationToken) =>
        PostNoContentAsync(relativePath, content: null, cancellationToken);

    private async Task PostNoContentAsync(
        string relativePath,
        object? content,
        CancellationToken cancellationToken)
    {
        using var response = await PostAsync(relativePath, content, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        await ThrowForFailureAsync(response, cancellationToken);
    }

    private Task<HttpResponseMessage> PostAsync(
        string relativePath,
        object? content,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, relativePath, content, cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        object? content,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpMonolithAccessClient));
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
            // Monolith maps board/ownership denials to 401; when the caller is already
            // authenticated, surface those as 403 Forbidden.
            var isAuthenticated = _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
            if (isAuthenticated)
                throw new AccessForbiddenException(detail);
            throw new UnauthorizedAccessException(detail);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new AccessForbiddenException(detail);

        throw new InvalidOperationException(
            $"Monolith access check failed ({(int)response.StatusCode}): {detail}");
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
