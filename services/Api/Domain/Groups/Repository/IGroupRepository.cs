using Api.Domain.Groups.Domain;

namespace Api.Domain.Groups.Repository
{
    public interface IGroupRepository
    {
        public Task CreateGroupAsync(Group group);
        public Task<Group?> GetGroupByIdAsync(long id);
        public Task<List<Group>> GetPublicGroupsAsync();
        public Task<List<Group>> GetGroupsByIdsAsync(IReadOnlyCollection<long> ids);
        public Task<IReadOnlyDictionary<long, string>> GetGroupNamesByIdsAsync(IEnumerable<long> ids);
        public Task<bool> ExistsGroupByIdAsync(long id);
        public Task UpdateGroupAsync(Group group);
        public Task DeleteGroupAsync(Group group);
    }
}
