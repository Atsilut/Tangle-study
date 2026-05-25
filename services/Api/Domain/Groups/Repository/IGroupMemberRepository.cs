using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Repository
{
    public interface IGroupMemberRepository
    {
        public Task AddMemberAsync(GroupMember member);
        public Task<GroupMember?> GetMemberAsync(long groupId, long userId);
        public Task<List<GroupMember>> GetMembersByGroupAsync(long groupId);
        public Task<List<GroupMember>> GetMembershipsByUserAsync(long userId);
        public Task<int> CountMembersAsync(long groupId);
        public Task UpdateMemberAsync(GroupMember member);
        public Task RemoveMemberAsync(GroupMember member);
        public Task RemoveAllByGroupAsync(long groupId);
        public Task RemoveAllByUserAsync(long userId);
    }
}
