using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Friendships.Domain
{
    [Index(nameof(UserLowId), nameof(UserHighId), IsUnique = true)]
    public class Friendship
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserLow))]
        public long UserLowId { get; private set; }
        public User? UserLow { get; private set; }

        [ForeignKey(nameof(UserHigh))]
        public long UserHighId { get; private set; }
        public User? UserHigh { get; private set; }

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
