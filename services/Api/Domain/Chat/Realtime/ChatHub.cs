using Api.Domain.Chat.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Domain.Chat.Realtime;

[Authorize]
public class ChatHub(ChatRoomService chatRoomService) : Hub
{
    public const string MessageCreatedEvent = "MessageCreated";
    public const string MessageEditedEvent = "MessageEdited";
    public const string MessageDeletedEvent = "MessageDeleted";

    private readonly ChatRoomService _chatRoomService = chatRoomService;

    public static string RoomGroup(long roomId) => $"room:{roomId}";

    public async Task JoinRoom(long roomId)
    {
        await _chatRoomService.EnsureUserIsParticipantAsync(roomId, GetUserId());
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(roomId));
    }

    public Task LeaveRoom(long roomId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(roomId));

    private long GetUserId() => long.Parse(Context.User?.FindFirst("sub")?.Value
        ?? throw new UnauthorizedAccessException("Unauthorized access"));
}
