using Location.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Location.Client;

internal sealed class HttpSocialClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<SocialClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpSocialClient)), ISocialClient
{
    protected override bool RemapAuthenticatedUnauthorizedToForbidden => true;

    public async Task<HashSet<long>> GetMutuallyBlockedUserIdsAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default)
    {
        var ids = otherUserIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        using var response = await PostAsync(
            "internal/social/blocks/mutual-ids",
            new SocialMutualBlocksRequestDto(userId, ids),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<SocialMutualBlocksResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Social mutual blocks response was empty.");

        return [.. payload.BlockedUserIds];
    }

    public async Task<bool> AnyBlockExistsBetweenUserAndOthersAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default)
    {
        var blocked = await GetMutuallyBlockedUserIdsAsync(userId, otherUserIds, cancellationToken);
        return blocked.Count > 0;
    }
}
