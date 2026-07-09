using Group.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Group.Client;

internal sealed class HttpCommunityClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<CommunityClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpCommunityClient)), ICommunityClient
{
    public Task DeleteAllByGroupAsync(long groupId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/community/groups/{groupId}/delete-all", cancellationToken);
}
