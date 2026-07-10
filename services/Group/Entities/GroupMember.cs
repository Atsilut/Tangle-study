using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Group.Entities
{
    [Index(nameof(GroupId), nameof(UserId), IsUnique = true)]
    public class GroupMember
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime JoinedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        public long GroupId { get; private set; }
        public Group? Group { get; private set; }

        public long UserId { get; private set; }

        public GroupRole Role { get; private set; }

        private GroupMember() { }

        public GroupMember(long groupId, long userId, GroupRole role)
        {
            GroupId = groupId;
            UserId = userId;
            Role = role;
        }

        public void ChangeRole(GroupRole role)
        {
            Role = role;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
