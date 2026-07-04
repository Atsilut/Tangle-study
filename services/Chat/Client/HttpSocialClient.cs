using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Chat.Config;
using Chat.Exceptions;
using Microsoft.Extensions.Options;

namespace Chat.Client;

internal sealed class HttpSocialClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<SocialClientOptions> options) : ISocialClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly SocialClientOptions _options = options.Value;

    public Task EnsureFriendshipExistsForUserPairAsync(long otherUserId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            "internal/social/friendships/validate-pair",
            new SocialOtherUserRequestDto(otherUserId),
            cancellationToken);

    public Task EnsureNoBlockBetweenUsersAsync(long otherUserId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            "internal/social/blocks/validate-between",
            new SocialOtherUserRequestDto(otherUserId),
            cancellationToken);

    public Task EnsureNoBlockBetweenUserAndOthersAsync(
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            "internal/social/blocks/validate-against-others",
            new SocialUserIdsRequestDto([.. otherUserIds]),
            cancellationToken);

    private async Task PostNoContentAsync(
        string relativePath,
        object? content,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, relativePath, content, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        await ThrowForFailureAsync(response, cancellationToken);
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

    private static async Task ThrowForFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await ReadProblemDetailAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(detail);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new EntityNotFoundException(detail);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException(detail);

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
