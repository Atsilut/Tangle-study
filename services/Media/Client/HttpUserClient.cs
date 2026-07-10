using Media.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Media.Client;

internal sealed class HttpUserClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<UsersOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpUserClient)), IUserClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/users/{userId}/exists", cancellationToken);
}
