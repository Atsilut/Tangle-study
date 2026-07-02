using Media.Global.Config;
using Media.Global.Exceptions;
using Microsoft.Extensions.Options;

namespace Media.Client;

public sealed class HttpMonolithAccessClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<MonolithOptions> options) : IMonolithAccessClient
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly MonolithOptions _options = options.Value;

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        PostAccessCheckAsync($"internal/access/users/{userId}/exists", cancellationToken);

    public Task EnsureCanViewPostMediaAsync(long postId, CancellationToken cancellationToken = default) =>
        PostAccessCheckAsync($"internal/access/posts/{postId}/media-view", cancellationToken);

    public Task EnsureCanViewCommentMediaAsync(long commentId, CancellationToken cancellationToken = default) =>
        PostAccessCheckAsync($"internal/access/comments/{commentId}/media-view", cancellationToken);

    public Task EnsureCanAccessChatMessageMediaAsync(long chatMessageId, CancellationToken cancellationToken = default) =>
        PostAccessCheckAsync($"internal/access/chat-messages/{chatMessageId}/media-view", cancellationToken);

    private async Task PostAccessCheckAsync(string relativePath, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpMonolithAccessClient));
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
            throw new UnauthorizedAccessException(await ReadBodyAsync(response, cancellationToken));

        throw new InvalidOperationException(
            $"Monolith access check failed ({(int)response.StatusCode}): {await ReadBodyAsync(response, cancellationToken)}");
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "Access denied" : body;
    }
}
