using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Location.Entities;

public class MapPin
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    public long? UserId { get; private set; }

    public long? DeletedUserId { get; private set; }

    public long? PostId { get; private set; }

    [Column(TypeName = "decimal(9,6)")]
    public decimal Latitude { get; private set; }

    [Column(TypeName = "decimal(9,6)")]
    public decimal Longitude { get; private set; }

    public long OwnerUserId => UserId ?? DeletedUserId!.Value;

    private MapPin() { }

    public MapPin(long userId, decimal latitude, decimal longitude, long? postId = null)
    {
        UserId = userId;
        Latitude = latitude;
        Longitude = longitude;
        PostId = postId;
    }

    public void DetachOwner(long userId)
    {
        DeletedUserId = userId;
        UserId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCoordinates(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
        UpdatedAt = DateTime.UtcNow;
    }
}
