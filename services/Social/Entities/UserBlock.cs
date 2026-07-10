using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Social.Entities;

public class UserBlock
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public long BlockerId { get; private set; }
    public long BlockedUserId { get; private set; }

    private UserBlock() { }

    public UserBlock(long blockerId, long blockedUserId)
    {
        if (blockerId == blockedUserId) throw new ArgumentException("Cannot block yourself.");
        BlockerId = blockerId;
        BlockedUserId = blockedUserId;
    }
}
