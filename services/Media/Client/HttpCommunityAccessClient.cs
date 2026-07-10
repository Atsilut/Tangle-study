using Media.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Media.Client;

internal sealed class HttpCommunityAccessClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<CommunityClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpCommunityAccessClient)),
        ICommunityAccessClient
{
    public Task EnsureCanViewPostMediaAsync(long postId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/community/{postId}/media-view", cancellationToken);

    public Task EnsureCanViewCommentMediaAsync(long commentId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/community/comments/{commentId}/media-view", cancellationToken);
}
