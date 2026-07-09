using Chat.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Chat.Client;

internal sealed class HttpGroupClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<GroupClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpGroupClient)), IGroupClient
{
    protected override bool RemapAuthenticatedUnauthorizedToForbidden => true;

    public Task EnsureGroupExistsAsync(long groupId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/group/{groupId}/exists", cancellationToken);

    public Task EnsureCallerIsGroupMemberAsync(long groupId, CancellationToken cancellationToken = default) =>
        GetNoContentAsync($"internal/group/{groupId}/membership/me", cancellationToken);

    public Task EnsureGroupMembersAsync(
        long groupId,
        IReadOnlyCollection<long> userIds,
        string membersErrorMessage,
        CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            $"internal/group/{groupId}/members/validate",
            new InternalGroupMembersRequestDto([.. userIds], membersErrorMessage),
            cancellationToken);

    public Task EnsureGroupMemberAsync(
        long groupId,
        long userId,
        string notFoundMessage,
        CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            $"internal/group/{groupId}/members/{userId}/validate",
            new InternalGroupMemberValidateRequestDto(notFoundMessage),
            cancellationToken);

    private sealed record InternalGroupMembersRequestDto(long[] UserIds, string? ErrorMessage = null);

    private sealed record InternalGroupMemberValidateRequestDto(string? NotFoundMessage = null);
}
