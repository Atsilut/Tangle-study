using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.UserBlocks.Domain
{
    [Index(nameof(BlockerId), nameof(BlockedUserId), IsUnique = true)]
    public class UserBlock
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Blocker))]
        public long BlockerId { get; private set; }
        public User? Blocker { get; private set; }

        [ForeignKey(nameof(BlockedUser))]
        public long BlockedUserId { get; private set; }
        public User? BlockedUser { get; private set; }

        private UserBlock() { }

        public UserBlock(long blockerId, long blockedUserId)
        {
            if (blockerId == blockedUserId) throw new ArgumentException("Cannot block yourself.");
            BlockerId = blockerId;
            BlockedUserId = blockedUserId;
        }
    }
}
