using Api.Domain.Chat.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Chat.Repository;

[Repository]
public class ChatMessageRepository(AppDbContext context) : IChatMessageRepository
{
    private readonly AppDbContext _context = context;

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

    public async Task<IReadOnlyDictionary<long, ChatMessage>> GetLatestChatMessagesByRoomIdsAsync(
        IReadOnlyCollection<long> roomIds)
    {
        if (roomIds.Count == 0) return new Dictionary<long, ChatMessage>();

        var roomIdList = roomIds.Distinct().ToList();
        var latestMessageIds = await _context.ChatMessages
            .Where(m => roomIdList.Contains(m.ChatRoomId))
            .GroupBy(m => m.ChatRoomId)
            .Select(g => g.Max(m => m.Id))
            .ToListAsync();

        if (latestMessageIds.Count == 0) return new Dictionary<long, ChatMessage>();

        var messages = await _context.ChatMessages
            .Where(m => latestMessageIds.Contains(m.Id))
            .ToListAsync();

        return messages.ToDictionary(m => m.ChatRoomId);
    }

    public Task DeleteChatMessageAsync(ChatMessage message)
    {
        _context.ChatMessages.Remove(message);
        return _context.SaveChangesAsync();
    }

    public async Task DetachSenderFromMessagesAsync(long senderUserId)
    {
        var messages = await _context.ChatMessages.Where(m => m.SenderUserId == senderUserId).ToListAsync();
        foreach (var message in messages)
            message.DetachSender(senderUserId);
        await _context.SaveChangesAsync();
    }
}
