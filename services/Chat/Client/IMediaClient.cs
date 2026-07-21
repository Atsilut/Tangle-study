namespace Chat.Client;

public interface IMediaClient
{
    Task LinkToChatMessageAsync(long chatMessageId, long senderUserId, long? mediaAssetId);

    Task UnlinkFromChatMessageAsync(long chatMessageId);

    Task<IReadOnlyDictionary<long, MediaAssetGetResponseDto?>> GetMediaByChatMessageIdsAsync(
        IReadOnlyCollection<long> chatMessageIds);

    Task<MediaAssetGetResponseDto?> GetMediaForChatMessageAsync(long chatMessageId);

    Task DeleteBlobStorageForChatMessageAsync(long chatMessageId);
}
