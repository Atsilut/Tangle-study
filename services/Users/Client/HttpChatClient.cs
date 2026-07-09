using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;
using Users.Config;

namespace Users.Client;

internal sealed class HttpChatClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<ChatClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpChatClient)), IChatClient
{
    public Task DetachUserOnDeletionAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/chat/users/{userId}/detach-on-deletion", cancellationToken);
}
