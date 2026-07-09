using Group.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Group.Client;

internal sealed class HttpSocialClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<SocialClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpSocialClient)), ISocialClient
{
    protected override bool RemapAuthenticatedUnauthorizedToForbidden => true;

    public async Task<bool> IsBlockedByAsync(
        long blockerUserId,
        long blockedUserId,
        CancellationToken cancellationToken = default)
    {
        using var response = await PostAsync(
            "internal/social/blocks/is-blocked-by",
            new SocialIsBlockedRequestDto(blockerUserId, blockedUserId),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<SocialIsBlockedResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Social is-blocked-by response was empty.");

        return payload.IsBlocked;
    }
}
