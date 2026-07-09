using Chat.Config;
using Microsoft.Extensions.Options;
using Tangle.AspNetCore.Http;

namespace Chat.Client;

internal sealed class HttpMediaClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<MediaClientOptions> options)
    : InternalHttpClientBase(httpClientFactory, httpContextAccessor, options.Value, nameof(HttpMediaClient)), IMediaClient
{
    public Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId)
    {
        if (mediaAssetId is null) return Task.CompletedTask;

        return PostNoContentAsync(
            "internal/media/link/chat-message",
            content: new { chatMessageId, senderUserId, mediaAssetId });
    }

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
}
