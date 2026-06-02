using Api.Domain.Groups.Domain;
using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Chat.Domain;

[Index(nameof(PlatformGroupId))]
[Index(nameof(UserLowId), nameof(UserHighId), IsUnique = true)]
public class ChatRoom
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public ChatRoomKind Kind { get; private set; }

    [MaxLength(200)]
    public string? Title { get; private set; }

    [ForeignKey(nameof(PlatformGroup))]
    public long? PlatformGroupId { get; private set; }
    public Group? PlatformGroup { get; private set; }

    [ForeignKey(nameof(CreatedByUser))]
    public long CreatedByUserId { get; private set; }
    public User? CreatedByUser { get; private set; }

    [ForeignKey(nameof(UserLow))]
    public long? UserLowId { get; private set; }
    public User? UserLow { get; private set; }

    [ForeignKey(nameof(UserHigh))]
    public long? UserHighId { get; private set; }
    public User? UserHigh { get; private set; }

    public ICollection<ChatRoomParticipant> Participants { get; private set; } = new List<ChatRoomParticipant>();

    private ChatRoom() { }

    private ChatRoom(
        ChatRoomKind kind,
        long createdByUserId,
        string? title,
        long? platformGroupId,
        long? userLowId,
        long? userHighId)
    {
        Kind = kind;
        CreatedByUserId = createdByUserId;
        Title = title;
        PlatformGroupId = platformGroupId;
        UserLowId = userLowId;
        UserHighId = userHighId;
    }

    public static ChatRoom CreateDirect(long userAId, long userBId, long createdByUserId)
    {
        if (userAId == userBId)
            throw new ArgumentException("Cannot create a direct chat room with yourself.");

        var userLowId = Math.Min(userAId, userBId);
        var userHighId = Math.Max(userAId, userBId);
        return new ChatRoom(
            ChatRoomKind.Direct,
            createdByUserId,
            title: null,
            platformGroupId: null,
            userLowId,
            userHighId);
    }

    public static ChatRoom CreateMulti(string? title, long createdByUserId) =>
        new(ChatRoomKind.Multi, createdByUserId, title, platformGroupId: null, userLowId: null, userHighId: null);

    public static ChatRoom CreatePlatformGroup(string? title, long platformGroupId, long createdByUserId) =>
        new(ChatRoomKind.PlatformGroup, createdByUserId, title, platformGroupId, userLowId: null, userHighId: null);

    public void TouchUpdatedAt() => UpdatedAt = DateTime.UtcNow;

    /// <summary>
    /// Converts a 1:1 room into a multi-user room so more participants can be added.
    /// Clears the direct user-pair columns (required by DB constraints).
    /// </summary>
    public void PromoteDirectToMulti()
    {
        if (Kind != ChatRoomKind.Direct)
            throw new InvalidOperationException("Only direct chat rooms can be promoted to multi.");

        Kind = ChatRoomKind.Multi;
        UserLowId = null;
        UserHighId = null;
        TouchUpdatedAt();
    }

    /// <summary>Platform-group chat rooms have a single owner who can invite; direct and multi do not.</summary>
    public bool HasRoomOwner => Kind == ChatRoomKind.PlatformGroup;

    public bool InvolvesDirectUser(long userId) =>
        Kind == ChatRoomKind.Direct && (UserLowId == userId || UserHighId == userId);

    public long OtherDirectPartyId(long userId)
    {
        if (Kind != ChatRoomKind.Direct)
            throw new InvalidOperationException("OtherDirectPartyId applies only to direct rooms.");
        if (UserLowId == userId) return UserHighId!.Value;
        if (UserHighId == userId) return UserLowId!.Value;
        throw new ArgumentException($"User {userId} is not part of this direct chat room.");
    }
}
