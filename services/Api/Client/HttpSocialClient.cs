using Api.Global.Config;
using Microsoft.Extensions.Options;

namespace Api.Client;

internal sealed class HttpSocialClient(
    IHttpClientFactory httpClientFactory,
    IOptions<SocialClientOptions> options) : ISocialClient
{
    public const string InternalSecretHeaderName = "X-Internal-Secret";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly SocialClientOptions _options = options.Value;

    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/social/users/{userId}/detach-on-deletion", cancellationToken);

    private async Task PostNoContentAsync(string relativePath, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpSocialClient));
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath);

        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation(InternalSecretHeaderName, _options.InternalSecret);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
