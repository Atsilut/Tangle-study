using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chat.Entities;

[Index(nameof(ChatRoomId), nameof(UserId), IsUnique = true)]
public class ChatRoomParticipant
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public DateTime JoinedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public long ChatRoomId { get; private set; }
    public ChatRoom? ChatRoom { get; private set; }

    public long UserId { get; private set; }

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
