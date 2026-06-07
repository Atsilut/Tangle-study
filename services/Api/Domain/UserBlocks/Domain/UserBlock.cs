using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Api.Domain.UserBlocks.Domain
{
    [Index(nameof(BlockerId), nameof(BlockedUserId), IsUnique = true)]
    public class UserBlock
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [SuppressMessage("Roslynator", "RCS1170", Justification = "Store-generated key; EF requires a writable accessor.")]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; } = DateTime.UtcNow;

        [ForeignKey(nameof(Blocker))]
        public long BlockerId { get; }
        public User? Blocker { get; }

        [ForeignKey(nameof(BlockedUser))]
        public long BlockedUserId { get; }
        public User? BlockedUser { get; }

        private UserBlock() { }

        public UserBlock(long blockerId, long blockedUserId)
        {
            if (blockerId == blockedUserId) throw new ArgumentException("Cannot block yourself.");
            BlockerId = blockerId;
            BlockedUserId = blockedUserId;
        }
    }
}
