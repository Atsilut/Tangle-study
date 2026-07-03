using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Chat.Entities;

public class ChatMessageEdit
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public long ChatMessageId { get; private set; }
    public ChatMessage? ChatMessage { get; private set; }

    [MaxLength(ChatMessage.MaxBodyLength)]
    public string Body { get; private set; } = string.Empty;

    public DateTime RecordedAt { get; private set; } = DateTime.UtcNow;

    private ChatMessageEdit() { }

    public ChatMessageEdit(long chatMessageId, string body)
    {
        if (body.Length > ChatMessage.MaxBodyLength)
            throw new ArgumentException($"Message body cannot exceed {ChatMessage.MaxBodyLength} characters.");

        ChatMessageId = chatMessageId;
        Body = body;
    }
}
