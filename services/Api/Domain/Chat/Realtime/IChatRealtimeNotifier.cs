using Api.Domain.Chat.Dto;

namespace Api.Domain.Chat.Realtime;

public interface IChatRealtimeNotifier
{
    public Task NotifyMessageCreatedAsync(long chatRoomId, ChatMessageGetResponseDto message);
}
