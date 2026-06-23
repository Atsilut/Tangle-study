using Api.Domain.Users.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Chat.Domain;

[Index(nameof(ChatMessageId), nameof(UserId), IsUnique = true)]
public class ChatMessageReceipt
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    [ForeignKey(nameof(ChatMessage))]
    public long ChatMessageId { get; private set; }
    public ChatMessage? ChatMessage { get; private set; }

    [ForeignKey(nameof(User))]
    public long UserId { get; private set; }
    public User? User { get; private set; }

    public DateTime SeenAt { get; private set; } = DateTime.UtcNow;

    private ChatMessageReceipt() { }

    public ChatMessageReceipt(long chatMessageId, long userId)
    {
        ChatMessageId = chatMessageId;
        UserId = userId;
    }
}
