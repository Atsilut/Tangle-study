using Api.Domain.Location.Domain;

namespace Api.Domain.Location.Repository;

public interface ILocationSessionRepository
{
    public Task CreateSessionAsync(LocationSession session);
    public Task<LocationSession?> GetSessionByIdAsync(long id);
    public Task<LocationSession?> GetActiveSessionForUserInGroupAsync(long userId, long groupId);
    public Task<List<LocationSession>> GetActiveSessionsForGroupAsync(long groupId);
    public Task<List<LocationSession>> GetAllActiveSessionsAsync();
    public Task UpdateSessionAsync(LocationSession session);
    public Task EndActiveSessionForUserInGroupAsync(long userId, long groupId);
    public Task EndAllActiveSessionsForUserAsync(long userId);
}
