using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Friendships.Domain
{
    [Index(nameof(RequesterId), nameof(AddresseeId), IsUnique = true)]
    public class FriendRequest
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

        public bool IsPending { get; private set; } = true;

        public bool IgnoredByBlock { get; private set; }

        private FriendRequest() { }

        public FriendRequest(long requesterId, long addresseeId)
        {
            if (requesterId == addresseeId) throw new ArgumentException("Cannot send a friend request to yourself.");
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
            IgnoredByBlock = false;
            UpdatedAt = DateTime.UtcNow;
        }

        public void IgnoreByBlock()
        {
            IsPending = false;
            IgnoredByBlock = true;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Unignore()
        {
            if (!IgnoredByBlock) return;

            IsPending = true;
            IgnoredByBlock = false;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
