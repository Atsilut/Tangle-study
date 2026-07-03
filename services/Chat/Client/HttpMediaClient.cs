using System.Net.Http.Json;
using Chat.Config;
using Microsoft.Extensions.Options;

namespace Chat.Client;

internal sealed class HttpMediaClient(IHttpClientFactory httpClientFactory, IOptions<MediaClientOptions> options)
    : IMediaClient
{
    public const string InternalSecretHeaderName = "X-Internal-Secret";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly MediaClientOptions _options = options.Value;

    public Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId) =>
        PostNoContentAsync("internal/media/link/chat-message", new { chatMessageId, senderUserId, mediaAssetId });

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

    public Task DeleteBlobStorageForChatMessageAsync(long chatMessageId) =>
        DeleteNoContentAsync($"internal/media/for-chat-message/{chatMessageId}");

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
