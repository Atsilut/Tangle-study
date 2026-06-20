using Api.Domain.Groups.Domain;
using Api.Domain.Users.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Location.Domain;

public class LocationSession
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; private set; }

    [ForeignKey(nameof(User))]
    public long? UserId { get; private set; }

    public long? DeletedUserId { get; private set; }

    public User? User { get; private set; }

    [ForeignKey(nameof(Group))]
    public long GroupId { get; private set; }

    public Group? Group { get; private set; }

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
