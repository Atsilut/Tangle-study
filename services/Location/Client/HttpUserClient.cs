using Location.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Location.Client;

internal sealed class HttpUserClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<UsersOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpUserClient)), IUserClient
{
    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/users/{userId}/validate", cancellationToken);

    public async Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<long, string>();

        var payload = await PostJsonAsync<InternalAccessNicknamesResponseDto>(
            "internal/users/nicknames",
            new InternalAccessUserIdsRequestDto(ids),
            cancellationToken)
            ?? throw new InvalidOperationException("Users nicknames response was empty.");

        return payload.Nicknames.ToDictionary(entry => entry.UserId, entry => entry.Nickname);
    }
}
