using Chat.Dto;
using Chat.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace Chat.Realtime;

[Service]
public class ChatRealtimeNotifier(IHubContext<ChatHub> hub) : IChatRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _hub = hub;

    public Task NotifyMessageCreatedAsync(long chatRoomId, ChatMessageGetResponseDto message) =>
        _hub.Clients
            .Group(ChatHub.RoomGroup(chatRoomId))
            .SendAsync(ChatHub.MessageCreatedEvent, message);

    public Task NotifyMessageEditedAsync(long chatRoomId, ChatMessageGetResponseDto message) =>
        _hub.Clients
            .Group(ChatHub.RoomGroup(chatRoomId))
            .SendAsync(ChatHub.MessageEditedEvent, message);

    public Task NotifyMessageDeletedAsync(long chatRoomId, ChatMessageGetResponseDto message) =>
        _hub.Clients
            .Group(ChatHub.RoomGroup(chatRoomId))
            .SendAsync(ChatHub.MessageDeletedEvent, message);
}
