using Chat.Entities;

namespace Chat.Repository;

public interface IChatRoomRepository
{
    Task CreateChatRoomAsync(ChatRoom room);
    Task<ChatRoom?> GetChatRoomByIdAsync(long id, bool includeParticipants = false);
    Task<ChatRoom?> GetDirectChatRoomForUserPairAsync(long userId, long otherUserId);
    Task<List<ChatRoom>> GetChatRoomsForUserAsync(long userId);
    Task<List<ChatRoom>> GetChatRoomsForPlatformGroupAsync(long platformGroupId);
    Task<bool> ExistsChatRoomParticipantAsync(long chatRoomId, long userId);
    Task<ChatRoomParticipant?> GetChatRoomParticipantAsync(long chatRoomId, long userId);
    Task AddChatRoomParticipantAsync(ChatRoomParticipant participant);
    Task RemoveChatRoomParticipantAsync(ChatRoomParticipant participant);
    Task TouchChatRoomUpdatedAtAsync(long chatRoomId);
    Task DetachCreatedByFromRoomsAsync(long userId);
    Task PromoteDirectRoomsForDeletedUserAsync(long userId);
    Task RemoveAllParticipantsForUserAsync(long userId);
}
