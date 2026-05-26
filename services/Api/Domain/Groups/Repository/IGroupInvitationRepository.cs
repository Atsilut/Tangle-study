using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Repository
{
    public interface IGroupInvitationRepository
    {
        public Task CreateInvitationAsync(GroupInvitation invitation);
        public Task<GroupInvitation?> GetByIdAsync(long id);
        public Task<GroupInvitation?> GetForUserAsync(long groupId, long inviteeId);
        public Task<GroupInvitation?> GetPendingForUserAsync(long groupId, long inviteeId);
        public Task<List<GroupInvitation>> GetPendingIncomingForInviteeAsync(long inviteeId);
        public Task<List<GroupInvitation>> GetIgnoredOutgoingForInviterAsync(long inviterId);
        public Task<List<GroupInvitation>> GetIgnoredIncomingForInviteeAsync(long inviteeId);
        public Task UpdateInvitationAsync(GroupInvitation invitation);
        public Task DeleteInvitationAsync(GroupInvitation invitation);
        public Task DeleteAllForUserAndGroupAsync(long groupId, long userId);
        public Task DeleteAllByGroupAsync(long groupId);
        public Task DeleteAllByUserAsync(long userId);
    }
}
