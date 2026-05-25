using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Repository
{
    public interface IGroupInvitationRepository
    {
        Task CreateInvitationAsync(GroupInvitation invitation);
        Task<GroupInvitation?> GetByIdAsync(long id);
        Task<GroupInvitation?> GetPendingForUserAsync(long groupId, long inviteeId);
        Task<List<GroupInvitation>> GetPendingIncomingForInviteeAsync(long inviteeId);
        Task<List<GroupInvitation>> GetIgnoredOutgoingForInviterAsync(long inviterId);
        Task<List<GroupInvitation>> GetIgnoredIncomingForInviteeAsync(long inviteeId);
        Task<List<GroupInvitation>> GetBetweenUsersAsync(long userId, long otherUserId);
        Task UpdateInvitationAsync(GroupInvitation invitation);
        Task DeleteInvitationAsync(GroupInvitation invitation);
        Task DeleteAllForUserAndGroupAsync(long groupId, long userId);
        Task DeleteAllByGroupAsync(long groupId);
        Task DeleteAllByUserAsync(long userId);
    }
}
