using Api.Domain.Users.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Chat.Domain;

public class ChatMessage
{
    public const int MaxBodyLength = 1000;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public DateTime SentAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public bool IsDeleted { get; private set; }

    [ForeignKey(nameof(ChatRoom))]
    public long ChatRoomId { get; private set; }
    public ChatRoom? ChatRoom { get; private set; }

    [ForeignKey(nameof(Sender))]
    public long? SenderUserId { get; private set; }

    public long? DeletedSenderUserId { get; private set; }

    public User? Sender { get; private set; }

    public long LogicalSenderUserId => SenderUserId ?? DeletedSenderUserId!.Value;

    [MaxLength(MaxBodyLength)]
    public string Body { get; private set; } = string.Empty;

    private ChatMessage() { }

    public ChatMessage(long chatRoomId, long senderUserId, string body)
    {
        if (body.Length > MaxBodyLength) throw new ArgumentException($"Message body cannot exceed {MaxBodyLength} characters.");

        ChatRoomId = chatRoomId;
        SenderUserId = senderUserId;
        Body = body;
        UpdatedAt = SentAt;
    }

    public void UpdateBody(string body)
    {
        if (IsDeleted) throw new InvalidOperationException("Deleted messages cannot be edited.");
        if (body.Length > MaxBodyLength) throw new ArgumentException($"Message body cannot exceed {MaxBodyLength} characters.");

        Body = body;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDeleted()
    {
        if (IsDeleted) return;

        IsDeleted = true;
        Body = string.Empty;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DetachSender(long senderUserId)
    {
        DeletedSenderUserId = senderUserId;
        SenderUserId = null;
    }
}
