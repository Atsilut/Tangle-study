using Chat.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Chat.Client;

internal sealed class HttpUserClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<UsersOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpUserClient)), IUserClient
{
    protected override bool RemapAuthenticatedUnauthorizedToForbidden => true;

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/users/{userId}/validate", cancellationToken);

    public Task EnsureUsersExistAsync(IReadOnlyCollection<long> userIds, CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            "internal/users/validate-exist",
            new InternalAccessUserIdsRequestDto([.. userIds]),
            cancellationToken);

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
