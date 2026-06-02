using Api.Domain.Chat.Dto;
using Api.Global.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace Api.Domain.Chat.Realtime;

[Service]
public class ChatRealtimeNotifier : IChatRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _hub;

    public ChatRealtimeNotifier(IHubContext<ChatHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyMessageCreatedAsync(long chatRoomId, ChatMessageGetResponseDto message) =>
        _hub.Clients
            .Group(ChatHub.RoomGroup(chatRoomId))
            .SendAsync(ChatHub.MessageCreatedEvent, message);
}
