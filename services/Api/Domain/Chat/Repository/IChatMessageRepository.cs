using Api.Domain.Chat.Domain;

namespace Api.Domain.Chat.Repository;

public interface IChatMessageRepository
{
    Task CreateChatMessageAsync(ChatMessage message);
    Task<ChatMessage?> GetChatMessageByIdAsync(long id);
    Task<List<ChatMessage>> GetChatMessagesForRoomAsync(long chatRoomId, long? beforeMessageId, int limit);
}
