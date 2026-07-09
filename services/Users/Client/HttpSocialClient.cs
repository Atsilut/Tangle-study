using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;
using Users.Config;

namespace Users.Client;

internal sealed class HttpSocialClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<SocialClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpSocialClient)), ISocialClient
{
    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/social/users/{userId}/detach-on-deletion", cancellationToken);
}
