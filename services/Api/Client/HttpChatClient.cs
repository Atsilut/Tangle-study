using Api.Global.Config;
using Microsoft.Extensions.Options;

namespace Api.Client;

internal sealed class HttpChatClient(
    IHttpClientFactory httpClientFactory,
    IOptions<ChatClientOptions> options) : IChatClient
{
    public const string InternalSecretHeaderName = "X-Internal-Secret";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ChatClientOptions _options = options.Value;

    public async Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpChatClient));
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"internal/chat/users/{userId}/detach-on-deletion");

        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation(InternalSecretHeaderName, _options.InternalSecret);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
