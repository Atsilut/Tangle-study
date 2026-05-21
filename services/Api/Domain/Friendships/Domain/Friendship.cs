using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Friendships.Domain
{
    [Index(nameof(RequesterId), nameof(AddresseeId), IsUnique = true)]
    public class Friendship
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Requester))]
        public long RequesterId { get; private set; }
        public User? Requester { get; private set; }

        [ForeignKey(nameof(Addressee))]
        public long AddresseeId { get; private set; }
        public User? Addressee { get; private set; }

        public FriendshipStatus Status { get; private set; }

        private Friendship() { }

        public Friendship(long requesterId, long addresseeId)
        {
            if (requesterId == addresseeId)
                throw new ArgumentException("Cannot create a friendship with yourself.");
            RequesterId = requesterId;
            AddresseeId = addresseeId;
            Status = FriendshipStatus.Pending;
        }

        public void Accept()
        {
            if (Status != FriendshipStatus.Pending)
                throw new ArgumentException($"Cannot accept a friendship in {Status} state.");
            Status = FriendshipStatus.Accepted;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Reject()
        {
            if (Status != FriendshipStatus.Pending)
                throw new ArgumentException($"Cannot reject a friendship in {Status} state.");
            Status = FriendshipStatus.Rejected;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool Involves(long userId) => RequesterId == userId || AddresseeId == userId;

        public long OtherPartyId(long userId)
        {
            if (RequesterId == userId) return AddresseeId;
            if (AddresseeId == userId) return RequesterId;
            throw new ArgumentException($"User {userId} is not part of this friendship.");
        }
    }
}
