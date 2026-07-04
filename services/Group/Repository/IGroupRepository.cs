using Group.Entities;
using GroupEntity = Group.Entities.Group;

namespace Group.Repository
{
    public interface IGroupRepository
    {
        public Task CreateGroupAsync(GroupEntity group);
        public Task<GroupEntity?> GetGroupByIdAsync(long id);
        public Task<List<GroupEntity>> GetPublicGroupsAsync();
        public Task<List<GroupEntity>> GetGroupsByIdsAsync(IReadOnlyCollection<long> ids);
        public Task<IReadOnlyDictionary<long, string>> GetGroupNamesByIdsAsync(IEnumerable<long> ids);
        public Task<bool> ExistsGroupByIdAsync(long id);
        public Task UpdateGroupAsync(GroupEntity group);
        public Task DeleteGroupAsync(GroupEntity group);
    }
}
