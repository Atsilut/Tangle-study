using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Location.Config;
using Location.Exceptions;
using Microsoft.Extensions.Options;

namespace Location.Client;

internal sealed class HttpMonolithAccessClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<MonolithOptions> options) : IMonolithAccessClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly MonolithOptions _options = options.Value;

    public Task EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/access/users/{userId}/validate", cancellationToken);

    public async Task<IReadOnlyDictionary<long, string>> GetNicknamesByUserIdsAsync(
        IEnumerable<long> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<long, string>();

        var response = await PostAsync(
            "internal/access/users/nicknames",
            new InternalAccessUserIdsRequestDto(ids),
            cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<InternalAccessNicknamesResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Monolith nicknames response was empty.");

        return payload.Nicknames.ToDictionary(entry => entry.UserId, entry => entry.Nickname);
    }

    public Task EnsurePostOwnerAsync(long postId, CancellationToken cancellationToken = default) =>
        PostNoContentAsync($"internal/access/posts/{postId}/validate-owner", cancellationToken);

    public async Task<HashSet<long>> GetViewablePostIdsAsync(
        IReadOnlyCollection<long> postIds,
        long? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var ids = postIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        var response = await PostAsync(
            "internal/access/posts/viewable-ids",
            new InternalAccessViewablePostsRequestDto(ids, viewerUserId),
            cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<InternalAccessViewablePostsResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Monolith viewable posts response was empty.");

        return [.. payload.ViewablePostIds];
    }

    public async Task<HashSet<long>> GetMutuallyBlockedUserIdsAsync(
        long userId,
        IReadOnlyCollection<long> otherUserIds,
        CancellationToken cancellationToken = default)
    {
        var ids = otherUserIds.Distinct().ToArray();
        if (ids.Length == 0) return [];

        var response = await PostAsync(
            "internal/access/users/blocks/mutual-ids",
            new InternalAccessMutualBlocksRequestDto(userId, ids),
            cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<InternalAccessMutualBlocksResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Monolith mutual blocks response was empty.");

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

    public Task EnsureGroupMemberAsync(
        long groupId,
        long userId,
        string notFoundMessage,
        CancellationToken cancellationToken = default)
    {
        _ = notFoundMessage;
        return PostNoContentAsync($"internal/access/groups/{groupId}/members/{userId}/validate", cancellationToken);
    }

    public async Task<bool> IsGroupMemberAsync(long groupId, long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await PostNoContentAsync($"internal/access/groups/{groupId}/members/{userId}/validate", cancellationToken);
            return true;
        }
        catch (UnauthorizedAccessException)
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
        using var response = await SendAsync(
            HttpMethod.Get,
            $"internal/access/groups/{groupId}/members/for-member",
            content: null,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<InternalAccessGroupMembersResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Monolith group members response was empty.");

        return [.. payload.Members.Select(m => new GroupMemberSummaryDto(m.UserId, m.Nickname))];
    }

    public async Task<IReadOnlyList<long>> GetGroupMemberUserIdsAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"internal/access/groups/{groupId}/member-ids",
            content: null,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowForFailureAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<InternalAccessGroupMemberIdsResponseDto>(
            SerializerOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Monolith group member ids response was empty.");

        return payload.MemberUserIds;
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
        var client = _httpClientFactory.CreateClient(nameof(HttpMonolithAccessClient));
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

    private static async Task ThrowForFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = await ReadProblemDetailAsync(response, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException(detail);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new EntityNotFoundException(detail);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException(detail);

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new AccessForbiddenException(detail);

        throw new InvalidOperationException(
            $"Monolith access check failed ({(int)response.StatusCode}): {detail}");
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
}
