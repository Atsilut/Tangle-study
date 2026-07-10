using Location.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Location.Client;

internal sealed class HttpCommunityAccessClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<CommunityClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpCommunityAccessClient)),
        ICommunityAccessClient
{
    public Task EnsurePostOwnerAsync(long postId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/community/{postId}/validate-owner", cancellationToken);

    public async Task<HashSet<long>> GetViewablePostIdsAsync(
        IReadOnlyCollection<long> postIds,
        long? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var ids = postIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        var payload = await PostJsonAsync<InternalCommunityViewableIdsResponseDto>(
            "internal/community/viewable-ids",
            new InternalCommunityViewableIdsRequestDto(ids, viewerUserId),
            cancellationToken)
            ?? throw new InvalidOperationException("Community viewable-ids response was empty.");

        return [.. payload.ViewablePostIds];
    }

    private sealed record InternalCommunityViewableIdsRequestDto(long[] PostIds, long? ViewerUserId);

    private sealed record InternalCommunityViewableIdsResponseDto(long[] ViewablePostIds);
}
