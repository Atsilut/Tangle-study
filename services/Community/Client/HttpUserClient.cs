using System.Net;
using Community.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Exceptions;
using Tangle.AspNetCore.Http;

namespace Community.Client;

internal sealed class HttpUserClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<UsersOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpUserClient)), IUserClient
{
    protected override bool RemapAuthenticatedUnauthorizedToForbidden => true;

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

    public async Task<long?> GetUserIdByNicknameAsync(string nickname, CancellationToken cancellationToken = default)
    {
        using var response = await PostAsync(
            "internal/users/by-nickname",
            new InternalAccessNicknameLookupRequestDto(nickname),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) await ThrowForFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<InternalAccessNicknameLookupResponseDto>(
            SerializerOptions,
            cancellationToken);
        return payload?.UserId;
    }
}
