using Chat.Db;
using Chat.Entities;
using Chat.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Chat.Repository;

[Repository]
public class ChatRoomRepository(ChatDbContext context) : IChatRoomRepository
{
    private readonly ChatDbContext _context = context;

    public Task CreateChatRoomAsync(ChatRoom room)
    {
        _context.ChatRooms.Add(room);
        return _context.SaveChangesAsync();
    }

    public Task<ChatRoom?> GetChatRoomByIdAsync(long id, bool includeParticipants = false)
    {
        var query = _context.ChatRooms.AsQueryable();
        if (includeParticipants) query = query.Include(r => r.Participants);
        return query.FirstOrDefaultAsync(r => r.Id == id);
    }

    public Task<ChatRoom?> GetDirectChatRoomForUserPairAsync(long userId, long otherUserId)
    {
        var userLowId = Math.Min(userId, otherUserId);
        var userHighId = Math.Max(userId, otherUserId);
        return _context.ChatRooms
            .Include(r => r.Participants)
            .FirstOrDefaultAsync(r =>
                r.Kind == ChatRoomKind.Direct
                && r.UserLowId == userLowId
                && r.UserHighId == userHighId);
    }

    public Task<List<ChatRoom>> GetChatRoomsForUserAsync(long userId) =>
        _context.ChatRooms
            .Include(r => r.Participants)
            .Where(r => r.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync();

    public Task<List<ChatRoom>> GetChatRoomsForPlatformGroupAsync(long platformGroupId) =>
        _context.ChatRooms
            .Include(r => r.Participants)
            .Where(r => r.PlatformGroupId == platformGroupId)
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync();

    public Task<bool> ExistsChatRoomParticipantAsync(long chatRoomId, long userId) =>
        _context.ChatRoomParticipants.AnyAsync(p => p.ChatRoomId == chatRoomId && p.UserId == userId);

    public Task<ChatRoomParticipant?> GetChatRoomParticipantAsync(long chatRoomId, long userId) =>
        _context.ChatRoomParticipants.FirstOrDefaultAsync(p =>
            p.ChatRoomId == chatRoomId && p.UserId == userId);

    public Task AddChatRoomParticipantAsync(ChatRoomParticipant participant)
    {
        _context.ChatRoomParticipants.Add(participant);
        return _context.SaveChangesAsync();
    }

    public Task RemoveChatRoomParticipantAsync(ChatRoomParticipant participant)
    {
        _context.ChatRoomParticipants.Remove(participant);
        return _context.SaveChangesAsync();
    }

    public async Task TouchChatRoomUpdatedAtAsync(long chatRoomId)
    {
        var room = await _context.ChatRooms.FindAsync(chatRoomId);
        if (room is null) return;
        room.TouchUpdatedAt();
        await _context.SaveChangesAsync();
    }

    public async Task DetachCreatedByFromRoomsAsync(long userId)
    {
        var rooms = await _context.ChatRooms.Where(r => r.CreatedByUserId == userId).ToListAsync();
        foreach (var room in rooms)
            room.DetachCreatedBy(userId);
        await _context.SaveChangesAsync();
    }

    public async Task PromoteDirectRoomsForDeletedUserAsync(long userId)
    {
        var rooms = await _context.ChatRooms
            .Where(r => r.Kind == ChatRoomKind.Direct && (r.UserLowId == userId || r.UserHighId == userId))
            .ToListAsync();
        foreach (var room in rooms)
            room.PromoteDirectToMulti();
        await _context.SaveChangesAsync();
    }

    public Task RemoveAllParticipantsForUserAsync(long userId) =>
        _context.ChatRoomParticipants.Where(p => p.UserId == userId).ExecuteDeleteAsync();
}
