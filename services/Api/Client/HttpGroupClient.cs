using Api.Global.Config;
using Microsoft.Extensions.Options;

namespace Api.Client;

internal sealed class HttpGroupClient(
    IHttpClientFactory httpClientFactory,
    IOptions<GroupClientOptions> options) : IGroupClient
{
    public const string InternalSecretHeaderName = "X-Internal-Secret";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly GroupClientOptions _options = options.Value;

    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/group/users/{userId}/detach-on-deletion", cancellationToken);

    private async Task PostNoContentAsync(string relativePath, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpGroupClient));
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);

        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation(InternalSecretHeaderName, _options.InternalSecret);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
