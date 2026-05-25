using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Repository
{
    public interface IGroupApplicationRepository
    {
        public Task CreateApplicationAsync(GroupApplication application);
        public Task<GroupApplication?> GetByIdAsync(long id);
        public Task<GroupApplication?> GetPendingForUserAsync(long groupId, long applicantId);
        public Task<List<GroupApplication>> GetPendingByGroupAsync(long groupId);
        public Task DeleteApplicationAsync(GroupApplication application);
        public Task DeleteAllForUserAndGroupAsync(long groupId, long userId);
        public Task DeleteAllByGroupAsync(long groupId);
        public Task DeleteAllByUserAsync(long userId);
    }
}
