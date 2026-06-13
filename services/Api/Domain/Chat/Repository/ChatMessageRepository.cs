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
        // Npgsql translates to DISTINCT ON; uses IX_ChatMessages_ChatRoomId_Id (ChatRoomId ASC, Id DESC).
        var messages = await _context.ChatMessages
            .Where(m => roomIdList.Contains(m.ChatRoomId))
            .GroupBy(m => m.ChatRoomId)
            .Select(g => g.OrderByDescending(m => m.Id).First())
            .ToListAsync();

        return messages.ToDictionary(m => m.ChatRoomId);
    }

    public Task SaveChatMessageAsync(ChatMessage message) => _context.SaveChangesAsync();

    public async Task<IReadOnlySet<long>> GetMessageIdsSeenByOtherParticipantsAsync(
        IReadOnlyCollection<long> messageIds)
    {
        if (messageIds.Count == 0) return new HashSet<long>();

        var idList = messageIds.Distinct().ToList();
        var messages = await _context.ChatMessages
            .Where(m => idList.Contains(m.Id))
            .Select(m => new { m.Id, m.SenderUserId })
            .ToListAsync();
        if (messages.Count == 0) return new HashSet<long>();

        var senderByMessageId = messages.ToDictionary(m => m.Id, m => m.SenderUserId);
        var receipts = await _context.ChatMessageReceipts
            .Where(r => idList.Contains(r.ChatMessageId))
            .Select(r => new { r.ChatMessageId, r.UserId })
            .ToListAsync();

        HashSet<long> seen = [];
        foreach (var receipt in receipts)
        {
            if (!senderByMessageId.TryGetValue(receipt.ChatMessageId, out var senderId)) continue;
            if (senderId is null || receipt.UserId == senderId.Value) continue;
            seen.Add(receipt.ChatMessageId);
        }

        return seen;
    }

    public async Task MarkMessagesSeenByUserAsync(long userId, IReadOnlyCollection<long> messageIds)
    {
        if (messageIds.Count == 0) return;

        var idList = messageIds.Distinct().ToList();
        var messages = await _context.ChatMessages
            .Where(m => idList.Contains(m.Id))
            .Select(m => new { m.Id, m.SenderUserId })
            .ToListAsync();
        var toMark = messages
            .Where(m => m.SenderUserId != userId)
            .Select(m => m.Id)
            .ToList();
        if (toMark.Count == 0) return;

        var existing = await _context.ChatMessageReceipts
            .Where(r => r.UserId == userId && toMark.Contains(r.ChatMessageId))
            .Select(r => r.ChatMessageId)
            .ToListAsync();
        var existingSet = existing.ToHashSet();

        foreach (var messageId in toMark)
        {
            if (existingSet.Contains(messageId)) continue;
            _context.ChatMessageReceipts.Add(new ChatMessageReceipt(messageId, userId));
        }

        if (_context.ChangeTracker.HasChanges()) await _context.SaveChangesAsync();
    }

    public void AddChatMessageEdit(ChatMessageEdit edit) => _context.ChatMessageEdits.Add(edit);

    public async Task<IReadOnlyDictionary<long, List<ChatMessageEdit>>> GetChatMessageEditsByMessageIdsAsync(
        IReadOnlyCollection<long> messageIds)
    {
        if (messageIds.Count == 0) return new Dictionary<long, List<ChatMessageEdit>>();

        var idList = messageIds.Distinct().ToList();
        var edits = await _context.ChatMessageEdits
            .Where(e => idList.Contains(e.ChatMessageId))
            .OrderBy(e => e.RecordedAt)
            .ToListAsync();

        return edits
            .GroupBy(e => e.ChatMessageId)
            .ToDictionary(g => g.Key, g => g.ToList());
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
