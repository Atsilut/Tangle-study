using Api.Domain.Chat.Domain;

namespace Api.Domain.Chat.Repository;

public interface IChatMessageRepository
{
    public Task CreateChatMessageAsync(ChatMessage message);
    public Task<ChatMessage?> GetChatMessageByIdAsync(long id);
    public Task<IReadOnlyDictionary<long, ChatMessage>> GetChatMessagesByIdsAsync(IReadOnlyCollection<long> ids);
    public Task<List<ChatMessage>> GetChatMessagesForRoomAsync(long chatRoomId, long? beforeMessageId, int limit);
    public Task<IReadOnlyDictionary<long, ChatMessage>> GetLatestChatMessagesByRoomIdsAsync(
        IReadOnlyCollection<long> roomIds);

    public Task SaveChatMessageAsync(ChatMessage message);

    public Task<IReadOnlySet<long>> GetMessageIdsSeenByOtherParticipantsAsync(
        IReadOnlyCollection<long> messageIds);

    public Task MarkMessagesSeenByUserAsync(long userId, IReadOnlyCollection<long> messageIds);

    public void AddChatMessageEdit(ChatMessageEdit edit);

    public Task<IReadOnlyDictionary<long, List<ChatMessageEdit>>> GetChatMessageEditsByMessageIdsAsync(
        IReadOnlyCollection<long> messageIds);

    public Task DeleteChatMessageAsync(ChatMessage message);

    public Task DetachSenderFromMessagesAsync(long senderUserId);
}
