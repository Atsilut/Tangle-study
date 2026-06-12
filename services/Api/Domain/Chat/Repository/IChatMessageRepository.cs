using Api.Domain.Chat.Domain;

namespace Api.Domain.Chat.Repository;

public interface IChatMessageRepository
{
    public Task CreateChatMessageAsync(ChatMessage message);
    public Task<ChatMessage?> GetChatMessageByIdAsync(long id);
    public Task<List<ChatMessage>> GetChatMessagesForRoomAsync(long chatRoomId, long? beforeMessageId, int limit);
    public Task<IReadOnlyDictionary<long, ChatMessage>> GetLatestChatMessagesByRoomIdsAsync(
        IReadOnlyCollection<long> roomIds);

    public Task DeleteChatMessageAsync(ChatMessage message);

    public Task DetachSenderFromMessagesAsync(long senderUserId);
}
