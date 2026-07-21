using Location.Entities;
using Microsoft.EntityFrameworkCore;
using Tangle.AspNetCore.Outbox;

namespace Location.Db;

public class LocationDbContext(DbContextOptions<LocationDbContext> options) : DbContext(options)
{
    public DbSet<MapPin> MapPins => Set<MapPin>();
    public DbSet<LocationSession> LocationSessions => Set<LocationSession>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("location");

        modelBuilder.Entity<MapPin>(entity =>
        {
            entity.ToTable("MapPins");
            entity.HasIndex(p => new { p.Latitude, p.Longitude });
            entity.HasIndex(p => p.UserId);
            entity.HasIndex(p => p.PostId);
        });

        modelBuilder.Entity<LocationSession>(entity =>
        {
            entity.ToTable("LocationSessions");
            entity.HasIndex(s => new { s.GroupId, s.EndedAt });
            entity.HasIndex(s => new { s.UserId, s.GroupId, s.EndedAt });
            entity.HasIndex(s => new { s.UserId, s.GroupId })
                .IsUnique()
                .HasFilter("\"EndedAt\" IS NULL");
        });

        modelBuilder.ConfigureOutbox();
    }
}
