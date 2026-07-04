using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Community.Config;
using Community.Exceptions;

namespace Community.Client;

internal sealed class HttpGroupClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<GroupClientOptions> options) : IGroupClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly GroupClientOptions _options = options.Value;

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

        var response = await PostAsync(
            "internal/group/boards/viewable-keys",
            new InternalGroupViewableBoardsRequestDto(
                [.. boardKeys.Select(k => new InternalGroupBoardKeyDto(k.GroupId, k.BoardId))]),
            cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<InternalGroupViewableBoardsResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Group viewable boards response was empty.");

        return [.. payload.Viewable.Select(b => (b.GroupId, b.BoardId))];
    }

    private Task PostNoContentAsync(string relativePath, CancellationToken cancellationToken) =>
        PostNoContentAsync(relativePath, content: null, cancellationToken);

    private async Task PostNoContentAsync(
        string relativePath,
        object? content,
        CancellationToken cancellationToken)
    {
        using var response = await PostAsync(relativePath, content, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        await ThrowForFailureAsync(response, cancellationToken);
    }

    private Task<HttpResponseMessage> PostAsync(
        string relativePath,
        object? content,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, relativePath, content, cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        object? content,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpGroupClient));
        using var request = new HttpRequestMessage(method, relativePath);

        if (content is not null)
            request.Content = JsonContent.Create(content, options: SerializerOptions);

        var authorization = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation("X-Internal-Secret", _options.InternalSecret);

        return await client.SendAsync(request, cancellationToken);
    }

    private async Task ThrowForFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await ReadProblemDetailAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(detail);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new EntityNotFoundException(detail);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var isAuthenticated = _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
            if (isAuthenticated)
                throw new AccessForbiddenException(detail);
            throw new UnauthorizedAccessException(detail);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new AccessForbiddenException(detail);

        throw new InvalidOperationException(
            $"Group access check failed ({(int)response.StatusCode}): {detail}");
    }

    private static async Task<string> ReadProblemDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
            return response.ReasonPhrase ?? "Access denied";

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("detail", out var detailElement)
                && detailElement.ValueKind == JsonValueKind.String)
                return detailElement.GetString() ?? body;
        }
        catch (JsonException)
        {
        }

        return body;
    }

    private sealed record InternalGroupBoardKeyDto(long GroupId, long BoardId);

    private sealed record InternalGroupViewableBoardsRequestDto(InternalGroupBoardKeyDto[] Boards);

    private sealed record InternalGroupViewableBoardsResponseDto(InternalGroupBoardKeyDto[] Viewable);
}
