using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Chat.Domain;

[Index(nameof(ChatRoomId), nameof(UserId), IsUnique = true)]
public class ChatRoomParticipant
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public DateTime JoinedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ChatRoom))]
    public long ChatRoomId { get; private set; }
    public ChatRoom? ChatRoom { get; private set; }

    [ForeignKey(nameof(User))]
    public long UserId { get; private set; }
    public User? User { get; private set; }

    public ChatRoomParticipantRole Role { get; private set; }

    private ChatRoomParticipant() { }

    public ChatRoomParticipant(long chatRoomId, long userId, ChatRoomParticipantRole role)
    {
        ChatRoomId = chatRoomId;
        UserId = userId;
        Role = role;
    }

    public bool IsOwner => Role == ChatRoomParticipantRole.Owner;
}
