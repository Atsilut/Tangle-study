using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Group.Entities
{
    [Index(nameof(GroupId), nameof(InviteeId), IsUnique = true)]
    public class GroupInvitation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        public long GroupId { get; private set; }
        public Group? Group { get; private set; }

        public long InviterId { get; private set; }
        public long InviteeId { get; private set; }

        public bool IsPending { get; private set; } = true;

        private GroupInvitation() { }

        public GroupInvitation(long groupId, long inviterId, long inviteeId)
        {
            if (inviterId == inviteeId) throw new ArgumentException("Cannot invite yourself.");
            GroupId = groupId;
            InviterId = inviterId;
            InviteeId = inviteeId;
        }

        public void Ignore()
        {
            IsPending = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
