using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Api.Domain.Friendships.Domain
{
    [Index(nameof(UserLowId), nameof(UserHighId), IsUnique = true)]
    public class Friendship
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [SuppressMessage("Roslynator", "RCS1170", Justification = "Store-generated key; EF requires a writable accessor.")]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserLow))]
        public long UserLowId { get; }
        public User? UserLow { get; }

        [ForeignKey(nameof(UserHigh))]
        public long UserHighId { get; }
        public User? UserHigh { get; }

        private Friendship() { }

        public Friendship(long userAId, long userBId)
        {
            if (userAId == userBId) throw new ArgumentException("Cannot create a friendship with yourself.");
            UserLowId = Math.Min(userAId, userBId);
            UserHighId = Math.Max(userAId, userBId);
        }

        public bool Involves(long userId) => UserLowId == userId || UserHighId == userId;

        public long OtherPartyId(long userId)
        {
            if (UserLowId == userId) return UserHighId;
            if (UserHighId == userId) return UserLowId;
            throw new ArgumentException($"User {userId} is not part of this friendship.");
        }
    }
}
