using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Api.Domain.Chat.Domain;

[Index(nameof(ChatRoomId), nameof(UserId), IsUnique = true)]
public class ChatRoomParticipant
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [SuppressMessage("Roslynator", "RCS1170", Justification = "Store-generated key; EF requires a writable accessor.")]
    public long Id { get; private set; }

    public DateTime JoinedAt { get; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ChatRoom))]
    public long ChatRoomId { get; }
    public ChatRoom? ChatRoom { get; }

    [ForeignKey(nameof(User))]
    public long UserId { get; }
    public User? User { get; }

    public ChatRoomParticipantRole Role { get; private set; }

    private ChatRoomParticipant() { }

    public ChatRoomParticipant(long chatRoomId, long userId, ChatRoomParticipantRole role)
    {
        ChatRoomId = chatRoomId;
        UserId = userId;
        Role = role;
    }

    public bool IsOwner => Role == ChatRoomParticipantRole.Owner;

    public void ChangeRole(ChatRoomParticipantRole role)
    {
        Role = role;
        UpdatedAt = DateTime.UtcNow;
    }
}
