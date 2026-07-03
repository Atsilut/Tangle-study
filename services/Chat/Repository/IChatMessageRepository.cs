using Chat.Entities;

namespace Chat.Repository;

public interface IChatMessageRepository
{
    Task CreateChatMessageAsync(ChatMessage message);
    Task<ChatMessage?> GetChatMessageByIdAsync(long id);
    Task<IReadOnlyDictionary<long, ChatMessage>> GetChatMessagesByIdsAsync(IReadOnlyCollection<long> ids);
    Task<List<ChatMessage>> GetChatMessagesForRoomAsync(long chatRoomId, long? beforeMessageId, int limit);
    Task<IReadOnlyDictionary<long, ChatMessage>> GetLatestChatMessagesByRoomIdsAsync(
        IReadOnlyCollection<long> roomIds);

    Task SaveChatMessageAsync(ChatMessage message);

    Task<IReadOnlySet<long>> GetMessageIdsSeenByOtherParticipantsAsync(
        IReadOnlyCollection<long> messageIds);

    Task MarkMessagesSeenByUserAsync(long userId, IReadOnlyCollection<long> messageIds);

    void AddChatMessageEdit(ChatMessageEdit edit);

    Task<IReadOnlyDictionary<long, List<ChatMessageEdit>>> GetChatMessageEditsByMessageIdsAsync(
        IReadOnlyCollection<long> messageIds);

    Task DeleteChatMessageAsync(ChatMessage message);

    Task DetachSenderFromMessagesAsync(long senderUserId);
}
