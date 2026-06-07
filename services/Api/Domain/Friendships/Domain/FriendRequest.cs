using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Api.Domain.Friendships.Domain
{
    [Index(nameof(RequesterId), nameof(AddresseeId), IsUnique = true)]
    public class FriendRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [SuppressMessage("Roslynator", "RCS1170", Justification = "Store-generated key; EF requires a writable accessor.")]
        public long Id { get; private set; }

        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

        [ForeignKey(nameof(Requester))]
        public long RequesterId { get; }
        public User? Requester { get; }

        [ForeignKey(nameof(Addressee))]
        public long AddresseeId { get; }
        public User? Addressee { get; }

        public bool IsPending { get; private set; } = true;

        private FriendRequest() { }

        public FriendRequest(long requesterId, long addresseeId)
        {
            if (requesterId == addresseeId)
                throw new ArgumentException("Cannot send a friend request to yourself.");
            RequesterId = requesterId;
            AddresseeId = addresseeId;
        }

        public bool IsUserInvolved(long userId) => RequesterId == userId || AddresseeId == userId;

        public long OtherPartyId(long userId)
        {
            if (RequesterId == userId) return AddresseeId;
            if (AddresseeId == userId) return RequesterId;
            throw new ArgumentException($"User {userId} is not part of this friend request.");
        }

        public void Ignore()
        {
            IsPending = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Unignore()
        {
            IsPending = true;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
