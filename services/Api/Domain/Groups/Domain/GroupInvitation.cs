using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Api.Domain.Groups.Domain
{
    [Index(nameof(GroupId), nameof(InviteeId), IsUnique = true)]
    public class GroupInvitation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [SuppressMessage("Roslynator", "RCS1170", Justification = "Store-generated key; EF requires a writable accessor.")]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Group))]
        public long GroupId { get; }
        public Group? Group { get; }

        [ForeignKey(nameof(Inviter))]
        public long InviterId { get; }
        public User? Inviter { get; }

        [ForeignKey(nameof(Invitee))]
        public long InviteeId { get; }
        public User? Invitee { get; }

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
