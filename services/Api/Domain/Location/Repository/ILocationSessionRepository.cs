using Api.Domain.Location.Domain;

namespace Api.Domain.Location.Repository;

public interface ILocationSessionRepository
{
    Task CreateSessionAsync(LocationSession session);
    Task<LocationSession?> GetSessionByIdAsync(long id);
    Task<LocationSession?> GetActiveSessionForUserInGroupAsync(long userId, long groupId);
    Task<List<LocationSession>> GetActiveSessionsForGroupAsync(long groupId);
    Task UpdateSessionAsync(LocationSession session);
    Task EndActiveSessionForUserInGroupAsync(long userId, long groupId);
    Task EndAllActiveSessionsForUserAsync(long userId);
}
