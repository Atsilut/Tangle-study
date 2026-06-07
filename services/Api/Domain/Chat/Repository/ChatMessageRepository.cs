using Api.Domain.Chat.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Chat.Repository;

[Repository]
public class ChatMessageRepository : IChatMessageRepository
{
    private readonly AppDbContext _context;

    public ChatMessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task CreateChatMessageAsync(ChatMessage message)
    {
        _context.ChatMessages.Add(message);
        return _context.SaveChangesAsync();
    }

    public Task<ChatMessage?> GetChatMessageByIdAsync(long id) =>
        _context.ChatMessages.FindAsync(id).AsTask();

    public async Task<List<ChatMessage>> GetChatMessagesForRoomAsync(
        long chatRoomId,
        long? beforeMessageId,
        int limit)
    {
        var query = _context.ChatMessages
            .Where(m => m.ChatRoomId == chatRoomId);

        if (beforeMessageId is not null)
            query = query.Where(m => m.Id < beforeMessageId.Value);

        var messages = await query
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync();

        messages.Reverse();
        return messages;
    }
}
