using Chat.Dto;

namespace Chat.Realtime;

public interface IChatRealtimeNotifier
{
    Task NotifyMessageCreatedAsync(long chatRoomId, ChatMessageGetResponseDto message);
    Task NotifyMessageEditedAsync(long chatRoomId, ChatMessageGetResponseDto message);
    Task NotifyMessageDeletedAsync(long chatRoomId, ChatMessageGetResponseDto message);
}
