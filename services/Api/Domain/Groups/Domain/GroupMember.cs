using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Groups.Domain
{
    [Index(nameof(GroupId), nameof(UserId), IsUnique = true)]
    public class GroupMember
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime JoinedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Group))]
        public long GroupId { get; private set; }
        public Group? Group { get; private set; }

        [ForeignKey(nameof(User))]
        public long UserId { get; private set; }
        public User? User { get; private set; }

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
