using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Group.Entities
{
    public class Group
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        public string Name { get; private set; }
        public string Description { get; private set; }
        public GroupVisibility Visibility { get; private set; }
        public GroupJoinPolicy JoinPolicy { get; private set; }
        public GroupInvitePolicy InvitePolicy { get; private set; }

        public ICollection<GroupMember> Members { get; private set; } = [];

        private Group() { }

        public Group(
            string name,
            string description,
            GroupVisibility visibility,
            GroupJoinPolicy joinPolicy = GroupJoinPolicy.Requestable,
            GroupInvitePolicy invitePolicy = GroupInvitePolicy.AdminsOnly)
        {
            Name = name;
            Description = description;
            Visibility = visibility;
            JoinPolicy = joinPolicy;
            InvitePolicy = invitePolicy;
        }

        public void UpdateDetails(
            string name,
            string description,
            GroupVisibility visibility,
            GroupJoinPolicy joinPolicy,
            GroupInvitePolicy invitePolicy)
        {
            Name = name;
            Description = description;
            Visibility = visibility;
            JoinPolicy = joinPolicy;
            InvitePolicy = invitePolicy;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
