using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;
using Users.Config;

namespace Users.Client;

internal sealed class HttpGroupClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<GroupClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpGroupClient)), IGroupClient
{
    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/group/users/{userId}/detach-on-deletion", cancellationToken);
}
