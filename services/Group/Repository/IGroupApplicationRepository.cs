using Group.Entities;

namespace Group.Repository
{
    public interface IGroupApplicationRepository
    {
        public Task CreateApplicationAsync(GroupApplication application);
        public Task<GroupApplication?> GetByIdAsync(long id);
        public Task<GroupApplication?> GetForUserAsync(long groupId, long applicantId);
        public Task<GroupApplication?> GetPendingForUserAsync(long groupId, long applicantId);
        public Task<List<GroupApplication>> GetPendingByGroupAsync(long groupId);
        public Task<List<GroupApplication>> GetIgnoredByGroupAsync(long groupId);
        public Task<List<GroupApplication>> GetPendingForApplicantAsync(long applicantId);
        public Task<List<GroupApplication>> GetIgnoredOutgoingForApplicantAsync(long applicantId);
        public Task UpdateApplicationAsync(GroupApplication application);
        public Task DeleteApplicationAsync(GroupApplication application);
        public Task DeleteAllForUserAndGroupAsync(long groupId, long userId);
        public Task DeleteAllByGroupAsync(long groupId);
        public Task DeleteAllByUserAsync(long userId);
    }
}
