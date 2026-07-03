using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chat.Entities;

[Index(nameof(ChatMessageId), nameof(UserId), IsUnique = true)]
public class ChatMessageReceipt
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public long ChatMessageId { get; private set; }
    public ChatMessage? ChatMessage { get; private set; }

    public long UserId { get; private set; }

    public DateTime SeenAt { get; private set; } = DateTime.UtcNow;

    private ChatMessageReceipt() { }

    public ChatMessageReceipt(long chatMessageId, long userId)
    {
        ChatMessageId = chatMessageId;
        UserId = userId;
    }
}
