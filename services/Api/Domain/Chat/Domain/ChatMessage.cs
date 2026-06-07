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

    [ForeignKey(nameof(ChatRoom))]
    public long ChatRoomId { get; private set; }
    public ChatRoom? ChatRoom { get; private set; }

    [ForeignKey(nameof(Sender))]
    public long SenderUserId { get; private set; }
    public User? Sender { get; private set; }

    [MaxLength(MaxBodyLength)]
    public string Body { get; private set; } = string.Empty;

    private ChatMessage() { }

    public ChatMessage(long chatRoomId, long senderUserId, string body)
    {
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Message body cannot be empty.");
        if (body.Length > MaxBodyLength) throw new ArgumentException($"Message body cannot exceed {MaxBodyLength} characters.");

        ChatRoomId = chatRoomId;
        SenderUserId = senderUserId;
        Body = body;
    }
}
