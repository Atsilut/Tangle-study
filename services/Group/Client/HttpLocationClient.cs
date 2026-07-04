using Microsoft.Extensions.Options;
using Group.Config;

namespace Group.Client;

internal sealed class HttpLocationClient(
    IHttpClientFactory httpClientFactory,
    IOptions<LocationClientOptions> options) : ILocationClient
{
    public const string InternalSecretHeaderName = "X-Internal-Secret";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly LocationClientOptions _options = options.Value;

    public Task EndSessionsForGroupAsync(long groupId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/location/groups/{groupId}/end-sessions", cancellationToken);

    private async Task PostNoContentAsync(string relativePath, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpLocationClient));
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);

        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation(InternalSecretHeaderName, _options.InternalSecret);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
