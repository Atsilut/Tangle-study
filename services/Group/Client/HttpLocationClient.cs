using Group.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Group.Client;

internal sealed class HttpLocationClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<LocationClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpLocationClient)), ILocationClient
{
    public Task EndSessionsForGroupAsync(long groupId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/location/groups/{groupId}/end-sessions", cancellationToken);
}
