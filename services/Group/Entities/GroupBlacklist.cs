using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Group.Entities
{
    [Index(nameof(GroupId), nameof(UserId), IsUnique = true)]
    public class GroupBlacklist
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        public long GroupId { get; private set; }
        public Group? Group { get; private set; }

        public long UserId { get; private set; }

        private GroupBlacklist() { }

        public GroupBlacklist(long groupId, long userId)
        {
            GroupId = groupId;
            UserId = userId;
        }
    }
}
