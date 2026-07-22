using Media.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Exceptions;
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

    public async Task<bool> PostExistsAsync(long postId, CancellationToken cancellationToken = default)
    {
        try
        {
            await PostNoContentAsync($"internal/community/posts/{postId}/exists", cancellationToken);
            return true;
        }
        catch (EntityNotFoundException)
        {
            return false;
        }
    }

    public async Task<bool> CommentExistsAsync(long commentId, CancellationToken cancellationToken = default)
    {
        try
        {
            await PostNoContentAsync($"internal/community/comments/{commentId}/exists", cancellationToken);
            return true;
        }
        catch (EntityNotFoundException)
        {
            return false;
        }
    }
}
