using Media.Config;
using Media.Exceptions;
using Microsoft.Extensions.Options;

namespace Media.Client;

internal sealed class HttpChatAccessClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<ChatClientOptions> options) : IChatAccessClient
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ChatClientOptions _options = options.Value;

    public Task EnsureCanAccessChatMessageMediaAsync(long chatMessageId, CancellationToken cancellationToken = default) =>
        PostAccessCheckAsync($"internal/chat/messages/{chatMessageId}/media-view", cancellationToken);

    private async Task PostAccessCheckAsync(string relativePath, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpChatAccessClient));
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);

        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation("X-Internal-Secret", _options.InternalSecret);

        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new EntityNotFoundException(await ReadBodyAsync(response, cancellationToken));

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException(await ReadBodyAsync(response, cancellationToken));

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new AccessForbiddenException(await ReadBodyAsync(response, cancellationToken));

        throw new InvalidOperationException(
            $"Chat access check failed ({(int)response.StatusCode}): {await ReadBodyAsync(response, cancellationToken)}");
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "Access denied" : body;
    }
}
