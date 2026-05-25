using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Repository
{
    public interface IGroupApplicationRepository
    {
        Task CreateApplicationAsync(GroupApplication application);
        Task<GroupApplication?> GetByIdAsync(long id);
        Task<GroupApplication?> GetPendingForUserAsync(long groupId, long applicantId);
        Task<List<GroupApplication>> GetPendingByGroupAsync(long groupId);
        Task<List<GroupApplication>> GetIgnoredByGroupAsync(long groupId);
        Task<List<GroupApplication>> GetPendingForApplicantAsync(long applicantId);
        Task<List<GroupApplication>> GetIgnoredOutgoingForApplicantAsync(long applicantId);
        Task UpdateApplicationAsync(GroupApplication application);
        Task DeleteApplicationAsync(GroupApplication application);
        Task DeleteAllForUserAndGroupAsync(long groupId, long userId);
        Task DeleteAllByGroupAsync(long groupId);
        Task DeleteAllByUserAsync(long userId);
    }
}
