using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;
using Users.Config;

namespace Users.Client;

internal sealed class HttpCommunityClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<CommunityClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpCommunityClient)),
        ICommunityClient
{
    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/community/users/{userId}/detach-on-deletion", cancellationToken);

    public Task DeleteAllByGroupAsync(long groupId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/community/groups/{groupId}/delete-all", cancellationToken);
}
