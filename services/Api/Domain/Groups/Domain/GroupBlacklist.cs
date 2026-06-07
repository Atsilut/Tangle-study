using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Api.Domain.Groups.Domain
{
    [Index(nameof(GroupId), nameof(UserId), IsUnique = true)]
    public class GroupBlacklist
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [SuppressMessage("Roslynator", "RCS1170", Justification = "Store-generated key; EF requires a writable accessor.")]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; } = DateTime.UtcNow;

        [ForeignKey(nameof(Group))]
        public long GroupId { get; }
        public Group? Group { get; }

        [ForeignKey(nameof(User))]
        public long UserId { get; }
        public User? User { get; }

        private GroupBlacklist() { }

        public GroupBlacklist(long groupId, long userId)
        {
            GroupId = groupId;
            UserId = userId;
        }
    }
}
