using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Repository
{
    public interface IGroupInvitationRepository
    {
        public Task CreateInvitationAsync(GroupInvitation invitation);
        public Task<GroupInvitation?> GetByIdAsync(long id);
        public Task<GroupInvitation?> GetPendingForUserAsync(long groupId, long inviteeId);
        public Task<List<GroupInvitation>> GetPendingForInviteeAsync(long inviteeId);
        public Task DeleteInvitationAsync(GroupInvitation invitation);
        public Task DeleteAllForUserAndGroupAsync(long groupId, long userId);
        public Task DeleteAllByGroupAsync(long groupId);
        public Task DeleteAllByUserAsync(long userId);
    }
}
