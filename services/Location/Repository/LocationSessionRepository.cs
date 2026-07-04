using Location.Entities;
using Location.Db;
using Location.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Location.Repository;

[Repository]
public class LocationSessionRepository(LocationDbContext context) : ILocationSessionRepository
{
    private readonly LocationDbContext _context = context;

    public Task CreateSessionAsync(LocationSession session)
    {
        _context.LocationSessions.Add(session);
        return _context.SaveChangesAsync();
    }

    public Task<LocationSession?> GetSessionByIdAsync(long id) =>
        _context.LocationSessions.FindAsync(id).AsTask();

    public Task<LocationSession?> GetActiveSessionForUserInGroupAsync(long userId, long groupId) =>
        _context.LocationSessions
            .Where(s => s.UserId == userId && s.GroupId == groupId && s.EndedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

    public Task<List<LocationSession>> GetActiveSessionsForGroupAsync(long groupId) =>
        _context.LocationSessions
            .Where(s => s.GroupId == groupId && s.EndedAt == null)
            .ToListAsync();

    public Task<List<LocationSession>> GetAllActiveSessionsAsync() =>
        _context.LocationSessions
            .Where(s => s.EndedAt == null)
            .ToListAsync();

    public Task UpdateSessionAsync(LocationSession session) =>
        _context.SaveChangesAsync();

    public async Task EndActiveSessionForUserInGroupAsync(long userId, long groupId)
    {
        var sessions = await _context.LocationSessions
            .Where(s => s.UserId == userId && s.GroupId == groupId && s.EndedAt == null)
            .ToListAsync();
        if (sessions.Count == 0) return;

        foreach (var session in sessions) session.End();
        await _context.SaveChangesAsync();
    }

    public async Task EndAllActiveSessionsForUserAsync(long userId)
    {
        var sessions = await _context.LocationSessions
            .Where(s => s.UserId == userId && s.EndedAt == null)
            .ToListAsync();
        if (sessions.Count == 0) return;

        foreach (var session in sessions) session.End();
        await _context.SaveChangesAsync();
    }
}
