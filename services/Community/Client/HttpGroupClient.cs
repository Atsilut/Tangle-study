using Community.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Exceptions;
using Tangle.AspNetCore.Http;

namespace Community.Client;

internal sealed class HttpGroupClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<GroupClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpGroupClient)), IGroupClient
{
    protected override bool RemapAuthenticatedUnauthorizedToForbidden => true;

    public Task EnsureCanViewBoardAsync(long groupId, long boardId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            $"internal/group/{groupId}/boards/{boardId}/validate-view",
            cancellationToken);

    public async Task<bool> TryCanViewBoardAsync(
        long groupId,
        long boardId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureCanViewBoardAsync(groupId, boardId, cancellationToken);
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

    public Task EnsureCanWritePostAsync(long groupId, long boardId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync(
            $"internal/group/{groupId}/boards/{boardId}/validate-write",
            cancellationToken);

    public async Task<HashSet<(long GroupId, long BoardId)>> ResolveViewableBoardKeysAsync(
        IReadOnlyCollection<(long GroupId, long BoardId)> boardKeys,
        CancellationToken cancellationToken = default)
    {
        if (boardKeys.Count == 0) return [];

        using var response = await PostAsync(
            "internal/group/boards/viewable-keys",
            new InternalGroupViewableBoardsRequestDto(
                [.. boardKeys.Select(k => new InternalGroupBoardKeyDto(k.GroupId, k.BoardId))]),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<InternalGroupViewableBoardsResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Group viewable boards response was empty.");

        return [.. payload.Viewable.Select(b => (b.GroupId, b.BoardId))];
    }

    private sealed record InternalGroupBoardKeyDto(long GroupId, long BoardId);

    private sealed record InternalGroupViewableBoardsRequestDto(InternalGroupBoardKeyDto[] Boards);

    private sealed record InternalGroupViewableBoardsResponseDto(InternalGroupBoardKeyDto[] Viewable);
}
