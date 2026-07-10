using Chat.Client;

namespace Chat.Tests.Infrastructure;

public sealed class FakeMediaClient : IMediaClient
{
    private long _nextId = 1;
    private readonly Dictionary<long, AssetState> _assets = [];

    private sealed class AssetState
    {
        public required MediaAssetGetResponseDto Dto { get; set; }
    }

    public long SeedReadyAsset(
        MediaIntendedContext context,
        string mimeType,
        string fileName,
        long storedSizeBytes,
        long uploaderUserId = 1)
    {
        var id = _nextId++;
        var kind = mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            ? MediaKind.Video
            : MediaKind.Image;
        var now = DateTime.UtcNow;
        _assets[id] = new AssetState
        {
            Dto = new MediaAssetGetResponseDto(
                id,
                kind,
                context,
                MediaProcessingStatus.Ready,
                mimeType,
                fileName,
                storedSizeBytes,
                storedSizeBytes,
                null,
                null,
                null,
                null,
                now,
                now),
        };
        return id;
    }

    public Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId)
    {
        if (mediaAssetId is long id)
            UpdateAsset(id, dto => dto with { ChatMessageId = chatMessageId });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByChatMessageIdsAsync(
        IReadOnlyCollection<long> chatMessageIds)
    {
        var result = chatMessageIds.ToDictionary(
            messageId => messageId,
            messageId => _assets.Values
                .Select(a => a.Dto)
                .FirstOrDefault(d => d.ChatMessageId == messageId));
        return Task.FromResult<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>>(result);
    }

    public Task<MediaAssetGetResponseDto?> GetMediaForChatMessageAsync(long chatMessageId) =>
        Task.FromResult(_assets.Values
            .Select(a => a.Dto)
            .FirstOrDefault(d => d.ChatMessageId == chatMessageId));

    public Task DeleteBlobStorageForChatMessageAsync(long chatMessageId) => Task.CompletedTask;

    private void UpdateAsset(long id, Func<MediaAssetGetResponseDto, MediaAssetGetResponseDto> update)
    {
        if (!_assets.TryGetValue(id, out var state))
            throw new InvalidOperationException($"Fake media asset {id} was not seeded.");
        state.Dto = update(state.Dto);
    }
}

public static class FakeMediaClientTestHelpers
{
    public static long SeedReadyAsset(
        FakeMediaClient mediaClient,
        MediaIntendedContext context,
        string mimeType,
        string fileName,
        long storedSizeBytes) =>
        mediaClient.SeedReadyAsset(context, mimeType, fileName, storedSizeBytes);
}
