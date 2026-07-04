using Microsoft.Extensions.Options;
using Group.Config;

namespace Group.Client;

internal sealed class HttpCommunityClient(
    IHttpClientFactory httpClientFactory,
    IOptions<CommunityClientOptions> options) : ICommunityClient
{
    public const string InternalSecretHeaderName = "X-Internal-Secret";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly CommunityClientOptions _options = options.Value;

    public Task DeleteAllByGroupAsync(long groupId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/community/groups/{groupId}/delete-all", cancellationToken);

    private async Task PostNoContentAsync(string relativePath, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpCommunityClient));
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);

        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation(InternalSecretHeaderName, _options.InternalSecret);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
