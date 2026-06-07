using Api.Domain.Users.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Api.Domain.Chat.Domain;

public class ChatMessage
{
    public const int MaxBodyLength = 1000;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [SuppressMessage("Roslynator", "RCS1170", Justification = "Store-generated key; EF requires a writable accessor.")]
    public long Id { get; private set; }

    public DateTime SentAt { get; } = DateTime.UtcNow;

    [ForeignKey(nameof(ChatRoom))]
    public long ChatRoomId { get; }
    public ChatRoom? ChatRoom { get; }

    [ForeignKey(nameof(Sender))]
    public long SenderUserId { get; }
    public User? Sender { get; }

    [MaxLength(MaxBodyLength)]
    public string Body { get; } = string.Empty;

    private ChatMessage() { }

    public ChatMessage(long chatRoomId, long senderUserId, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Message body cannot be empty.");
        if (body.Length > MaxBodyLength)
            throw new ArgumentException($"Message body cannot exceed {MaxBodyLength} characters.");

        ChatRoomId = chatRoomId;
        SenderUserId = senderUserId;
        Body = body;
    }
}
