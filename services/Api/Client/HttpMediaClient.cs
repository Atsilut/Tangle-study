using System.Net.Http.Json;
using Api.Client;
using Api.Global.Config;
using Microsoft.Extensions.Options;

namespace Api.Client;

internal sealed class HttpMediaClient(IHttpClientFactory httpClientFactory, IOptions<MediaClientOptions> options)
    : IMediaClient
{
    public const string InternalSecretHeaderName = "X-Internal-Secret";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly MediaClientOptions _options = options.Value;

    public Task LinkToPostAsync(long postId, long uploaderUserId, IReadOnlyList<long>? mediaAssetIds) =>
        PostNoContentAsync("internal/media/link/post", new { postId, uploaderUserId, mediaAssetIds });

    public Task PatchPostMediaAsync(
        long postId,
        long uploaderUserId,
        IReadOnlyList<long>? addMediaAssetIds,
        IReadOnlyList<long>? removeMediaAssetIds) =>
        PostNoContentAsync(
            "internal/media/link/post/patch",
            new { postId, uploaderUserId, addMediaAssetIds, removeMediaAssetIds });

    public Task LinkToCommentAsync(long commentId, long uploaderUserId, long? mediaAssetId)
    {
        if (mediaAssetId is null) return Task.CompletedTask;

        return PostNoContentAsync(
            "internal/media/link/comment",
            new { commentId, uploaderUserId, mediaAssetId });
    }

    public Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId)
    {
        if (mediaAssetId is null) return Task.CompletedTask;

        return PostNoContentAsync(
            "internal/media/link/chat-message",
            new { chatMessageId, senderUserId, mediaAssetId });
    }

    public async Task<IReadOnlyDictionary<long, IReadOnlyList<MediaAssetGetResponseDto>>> GetMediaByPostIdsAsync(
        IReadOnlyCollection<long> postIds)
    {
        var result = await PostJsonAsync<Dictionary<long, List<MediaAssetGetResponseDto>>>(
            "internal/media/batch/by-post-ids",
            new { postIds }) ?? [];

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MediaAssetGetResponseDto>)pair.Value);
    }

    public async Task<IReadOnlyList<MediaAssetGetResponseDto>> GetMediaForPostAsync(long postId)
    {
        var result = await GetMediaByPostIdsAsync([postId]);
        return result.GetValueOrDefault(postId) ?? [];
    }

    public async Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByCommentIdsAsync(
        IReadOnlyCollection<long> commentIds) =>
        await PostJsonAsync<Dictionary<long, MediaAssetGetResponseDto?>>(
            "internal/media/batch/by-comment-ids",
            new { commentIds }) ?? new Dictionary<long, MediaAssetGetResponseDto?>();

    public async Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByChatMessageIdsAsync(
        IReadOnlyCollection<long> chatMessageIds) =>
        await PostJsonAsync<Dictionary<long, MediaAssetGetResponseDto?>>(
            "internal/media/batch/by-chat-message-ids",
            new { chatMessageIds }) ?? new Dictionary<long, MediaAssetGetResponseDto?>();

    public async Task<MediaAssetGetResponseDto?> GetMediaForChatMessageAsync(long chatMessageId)
    {
        var result = await GetMediaByChatMessageIdsAsync([chatMessageId]);
        return result.GetValueOrDefault(chatMessageId);
    }

    public Task DeleteBlobStorageForPostAsync(long postId) =>
        DeleteNoContentAsync($"internal/media/for-post/{postId}");

    public Task DeleteBlobStorageForPostsAsync(IReadOnlyCollection<long> postIds) =>
        DeleteWithBodyAsync("internal/media/for-posts", new { postIds });

    public Task DeleteBlobStorageForCommentAsync(long commentId) =>
        DeleteNoContentAsync($"internal/media/for-comment/{commentId}");

    public Task DeleteBlobStorageForCommentsAsync(IReadOnlyCollection<long> commentIds) =>
        DeleteWithBodyAsync("internal/media/for-comments", new { commentIds });

    public Task DeleteBlobStorageForChatMessageAsync(long chatMessageId) =>
        DeleteNoContentAsync($"internal/media/for-chat-message/{chatMessageId}");

    public Task DetachUploaderFromDeletedUserAsync(long uploaderId) =>
        PostNoContentAsync($"internal/media/detach-uploader/{uploaderId}", new { });

    private async Task PostNoContentAsync(string relativePath, object body)
    {
        using var response = await SendAsync(HttpMethod.Post, relativePath, body);
        response.EnsureSuccessStatusCode();
    }

    private async Task<T?> PostJsonAsync<T>(string relativePath, object body)
    {
        using var response = await SendAsync(HttpMethod.Post, relativePath, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    private async Task DeleteNoContentAsync(string relativePath)
    {
        using var response = await SendAsync(HttpMethod.Delete, relativePath, body: null);
        response.EnsureSuccessStatusCode();
    }

    private async Task DeleteWithBodyAsync(string relativePath, object body)
    {
        using var response = await SendAsync(HttpMethod.Delete, relativePath, body);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativePath, object? body)
    {
        var client = _httpClientFactory.CreateClient(nameof(HttpMediaClient));
        using var request = new HttpRequestMessage(method, relativePath);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        if (!string.IsNullOrWhiteSpace(_options.InternalSecret))
            request.Headers.TryAddWithoutValidation(InternalSecretHeaderName, _options.InternalSecret);

        return await client.SendAsync(request);
    }
}
