using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Location.Entities;

public class LocationSession
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; private set; }

    public long? UserId { get; private set; }

    public long? DeletedUserId { get; private set; }

    public long GroupId { get; private set; }

    public bool IsActive => EndedAt is null;

    public long OwnerUserId => UserId ?? DeletedUserId!.Value;

    private LocationSession() { }

    public LocationSession(long userId, long groupId)
    {
        UserId = userId;
        GroupId = groupId;
    }

    public void End()
    {
        EndedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DetachOwner(long userId)
    {
        DeletedUserId = userId;
        UserId = null;
        EndedAt ??= DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
