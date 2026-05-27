using Api.Domain.Chat.Domain;

namespace Api.Domain.Chat.Repository;

public interface IChatRoomRepository
{
    public Task CreateChatRoomAsync(ChatRoom room);
    public Task<ChatRoom?> GetChatRoomByIdAsync(long id, bool includeParticipants = false);
    public Task<ChatRoom?> GetDirectChatRoomForUserPairAsync(long userId, long otherUserId);
    public Task<List<ChatRoom>> GetChatRoomsForUserAsync(long userId);
    public Task<List<ChatRoom>> GetChatRoomsForPlatformGroupAsync(long platformGroupId);
    public Task<bool> ExistsChatRoomParticipantAsync(long chatRoomId, long userId);
    public Task<ChatRoomParticipant?> GetChatRoomParticipantAsync(long chatRoomId, long userId);
    public Task AddChatRoomParticipantAsync(ChatRoomParticipant participant);
    public Task RemoveChatRoomParticipantAsync(ChatRoomParticipant participant);
    public Task TouchChatRoomUpdatedAtAsync(long chatRoomId);
}
