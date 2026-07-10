using Location.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Exceptions;
using Tangle.AspNetCore.Http;

namespace Location.Client;

internal sealed class HttpGroupClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<GroupClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpGroupClient)), IGroupClient
{
    protected override bool RemapAuthenticatedUnauthorizedToForbidden => true;

    public Task EnsureGroupMemberAsync(
        long groupId,
        long userId,
        string notFoundMessage,
        CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            $"internal/group/{groupId}/members/{userId}/validate",
            new InternalGroupMemberValidateRequestDto(notFoundMessage),
            cancellationToken);

    public async Task<bool> IsGroupMemberAsync(long groupId, long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureGroupMemberAsync(groupId, userId, "Group not found", cancellationToken);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (AccessForbiddenException)
        {
            return false;
        }
        catch (EntityNotFoundException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<GroupMemberSummaryDto>> GetGroupMembersForMemberAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetAsync($"internal/group/{groupId}/members/for-member", cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<InternalGroupMembersResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Group members response was empty.");

        return [.. payload.Members.Select(m => new GroupMemberSummaryDto(m.UserId, m.Nickname))];
    }

    public async Task<IReadOnlyList<long>> GetGroupMemberUserIdsAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetAsync($"internal/group/{groupId}/member-ids", cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<InternalGroupMemberIdsResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Group member ids response was empty.");

        return payload.MemberUserIds;
    }

    private sealed record InternalGroupMemberValidateRequestDto(string? NotFoundMessage = null);

    private sealed record InternalGroupMemberEntryDto(long UserId, string Nickname);

    private sealed record InternalGroupMembersResponseDto(IReadOnlyList<InternalGroupMemberEntryDto> Members);

    private sealed record InternalGroupMemberIdsResponseDto(long[] MemberUserIds);
}
