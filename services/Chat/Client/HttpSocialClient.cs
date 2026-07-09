using Chat.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Chat.Client;

internal sealed class HttpSocialClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<SocialClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpSocialClient)), ISocialClient
{
    protected override bool RemapAuthenticatedUnauthorizedToForbidden => true;

    public Task EnsureFriendshipExistsForUserPairAsync(
        long userId,
        long otherUserId,
        CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            "internal/social/friendships/validate-pair",
            new SocialUserPairRequestDto(userId, otherUserId),
            cancellationToken);

    public Task EnsureNoBlockBetweenUsersAsync(
        long userId,
        long otherUserId,
        CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            "internal/social/blocks/validate-between",
            new SocialUserPairRequestDto(userId, otherUserId),
            cancellationToken);

    public Task EnsureNoBlockBetweenUserAndOthersAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            "internal/social/blocks/validate-against-others",
            new SocialMutualBlocksRequestDto(userId, [.. otherUserIds]),
            cancellationToken);
}
