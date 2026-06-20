using Api.Domain.Posts.Domain;
using Api.Domain.Users.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Domain.Location.Domain;

public class MapPin
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; private set; }

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    [ForeignKey(nameof(User))]
    public long? UserId { get; private set; }

    public long? DeletedUserId { get; private set; }

    public User? User { get; private set; }

    [ForeignKey(nameof(Post))]
    public long? PostId { get; private set; }

    public Post? Post { get; private set; }

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
