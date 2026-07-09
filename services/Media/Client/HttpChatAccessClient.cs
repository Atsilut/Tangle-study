using Media.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Media.Client;

internal sealed class HttpChatAccessClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<ChatClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpChatAccessClient)), IChatAccessClient
{
    public Task EnsureCanAccessChatMessageMediaAsync(long chatMessageId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/chat/messages/{chatMessageId}/media-view", cancellationToken);
}
