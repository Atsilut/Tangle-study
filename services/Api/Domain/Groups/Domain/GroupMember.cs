using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Api.Domain.Groups.Domain
{
    [Index(nameof(GroupId), nameof(UserId), IsUnique = true)]
    public class GroupMember
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [SuppressMessage("Roslynator", "RCS1170", Justification = "Store-generated key; EF requires a writable accessor.")]
        public long Id { get; private set; }

        public DateTime JoinedAt { get; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Group))]
        public long GroupId { get; }
        public Group? Group { get; }

        [ForeignKey(nameof(User))]
        public long UserId { get; }
        public User? User { get; }

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
