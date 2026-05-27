using Api.Domain.Chat.Domain;
using Api.Global.Db;
using Api.Global.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Domain.Chat.Repository;

[Repository]
public class ChatRoomRepository : IChatRoomRepository
{
    private readonly AppDbContext _context;

    public ChatRoomRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task CreateChatRoomAsync(ChatRoom room)
    {
        _context.ChatRooms.Add(room);
        await _context.SaveChangesAsync();
    }

    public async Task<ChatRoom?> GetChatRoomByIdAsync(long id, bool includeParticipants = false)
    {
        var query = _context.ChatRooms.AsQueryable();
        if (includeParticipants)
            query = query.Include(r => r.Participants);
        return await query.FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<ChatRoom?> GetDirectChatRoomForUserPairAsync(long userId, long otherUserId)
    {
        var userLowId = Math.Min(userId, otherUserId);
        var userHighId = Math.Max(userId, otherUserId);
        return await _context.ChatRooms.FirstOrDefaultAsync(r =>
            r.Kind == ChatRoomKind.Direct
            && r.UserLowId == userLowId
            && r.UserHighId == userHighId);
    }

    public async Task<List<ChatRoom>> GetChatRoomsForUserAsync(long userId) =>
        await _context.ChatRooms
            .Where(r => r.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync();

    public async Task<List<ChatRoom>> GetChatRoomsForPlatformGroupAsync(long platformGroupId) =>
        await _context.ChatRooms
            .Where(r => r.PlatformGroupId == platformGroupId)
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync();

    public async Task<bool> ExistsChatRoomParticipantAsync(long chatRoomId, long userId) =>
        await _context.ChatRoomParticipants.AnyAsync(p => p.ChatRoomId == chatRoomId && p.UserId == userId);

    public async Task<ChatRoomParticipant?> GetChatRoomParticipantAsync(long chatRoomId, long userId) =>
        await _context.ChatRoomParticipants.FirstOrDefaultAsync(p =>
            p.ChatRoomId == chatRoomId && p.UserId == userId);

    public async Task AddChatRoomParticipantAsync(ChatRoomParticipant participant)
    {
        _context.ChatRoomParticipants.Add(participant);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveChatRoomParticipantAsync(ChatRoomParticipant participant)
    {
        _context.ChatRoomParticipants.Remove(participant);
        await _context.SaveChangesAsync();
    }

    public async Task TouchChatRoomUpdatedAtAsync(long chatRoomId)
    {
        var room = await _context.ChatRooms.FindAsync(chatRoomId);
        if (room is null) return;
        room.TouchUpdatedAt();
        await _context.SaveChangesAsync();
    }
}
