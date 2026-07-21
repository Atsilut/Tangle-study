using Chat.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chat.Tests.Infrastructure;

internal static class ChatDbContextTestExtensions
{
    public static async Task ClearAllChatDataAsync(this ChatWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync();
        await db.ChatMessageEdits.ExecuteDeleteAsync();
        await db.ChatMessageReceipts.ExecuteDeleteAsync();
        await db.ChatMessages.ExecuteDeleteAsync();
        await db.ChatRoomParticipants.ExecuteDeleteAsync();
        await db.ChatRooms.ExecuteDeleteAsync();
    }
}
